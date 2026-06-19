using System.Text;
using System.Text.Json;
using TranslatorApi.Models;
using Microsoft.Extensions.Hosting;

namespace TranslatorApi.Services;

public class TranslationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TranslationService> _logger;
    private readonly string _historyFolder;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private List<LanguageInfo>? _languagesCache;
    private DateTime _languagesCachedAt;
    private static readonly TimeSpan _languagesCacheTtl = TimeSpan.FromMinutes(10);

    public TranslationService(HttpClient httpClient, IConfiguration config, IHostEnvironment env, ILogger<TranslationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _historyFolder = ResolveFolder(config["Storage:HistoricoFolder"], env.ContentRootPath);
    }

    private static string ResolveFolder(string? configured, string contentRootPath)
    {
        if (!string.IsNullOrWhiteSpace(configured) && Path.IsPathRooted(configured))
        {
            Directory.CreateDirectory(configured);
            return configured;
        }

        var folder = Path.Combine(contentRootPath, !string.IsNullOrWhiteSpace(configured) ? configured : "JSON history");
        Directory.CreateDirectory(folder);
        return folder;
    }

    private string? ResolveSafePath(string fileName)
    {
        var safe = Path.GetFileName(fileName);
        if (!safe.StartsWith("traducao_") || !safe.EndsWith(".json"))
            return null;

        var path = Path.Combine(_historyFolder, safe);
        return File.Exists(path) ? path : null;
    }

    public async Task<TranslationRecord> TranslateAsync(TranslationRequest request)
    {
        var record = new TranslationRecord
        {
            RequestDate = DateTime.UtcNow,
            SourceLanguage = request.SourceLanguage,
            OriginalText = request.HtmlContent
        };

        foreach (var targetLang in request.TargetLanguages)
        {
            _logger.LogInformation("Translating to [{lang}]...", targetLang);
            try
            {
                var translated = await CallLibreTranslateAsync(request.HtmlContent, request.SourceLanguage, targetLang);
                record.Translations.Add(new TranslationResult { Language = targetLang, TranslatedText = translated });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error translating to {lang}", targetLang);
                record.Translations.Add(new TranslationResult { Language = targetLang, TranslatedText = $"[ERRO: {ex.Message}]" });
            }
        }

        return record;
    }

    public async Task<string> PublishAsync(TranslationRecord record)
    {
        var langs = string.Join("-", record.Translations.Select(t => t.Language));
        var timestamp = record.RequestDate.ToString("yyyyMMdd_HHmmss");
        var fileName = $"traducao_{timestamp}_{record.SourceLanguage}-{langs}.json";
        var filePath = Path.Combine(_historyFolder, fileName);

        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(record, _jsonOptions), Encoding.UTF8);

        _logger.LogInformation("Saved: '{path}'", filePath);
        return fileName;
    }

    public List<HistoryEntry> ListHistory()
    {
        return Directory
            .EnumerateFiles(_historyFolder, "traducao_*.json")
            .Select(path => new FileInfo(path))
            .OrderByDescending(fi => fi.LastWriteTimeUtc)
            .Select(fi => new HistoryEntry { FileName = fi.Name, CreatedAt = fi.LastWriteTimeUtc, SizeBytes = fi.Length })
            .ToList();
    }

    public async Task<(byte[] bytes, string fileName)?> DownloadAsync(string fileName)
    {
        var path = ResolveSafePath(fileName);
        if (path is null) return null;

        return (await File.ReadAllBytesAsync(path), Path.GetFileName(path));
    }

    public async Task<TranslationRecord?> GetRecordAsync(string fileName)
    {
        var path = ResolveSafePath(fileName);
        if (path is null) return null;

        var json = await File.ReadAllTextAsync(path, Encoding.UTF8);
        return JsonSerializer.Deserialize<TranslationRecord>(json, _jsonOptions);
    }

    public bool Delete(string fileName)
    {
        var path = ResolveSafePath(fileName);
        if (path is null) return false;

        File.Delete(path);
        _logger.LogInformation("Deleted: '{path}'", path);
        return true;
    }

    public async Task<List<LanguageInfo>> GetLanguagesAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && _languagesCache != null && DateTime.UtcNow - _languagesCachedAt < _languagesCacheTtl)
            return _languagesCache;

        var response = await _httpClient.GetAsync("/languages");
        if (!response.IsSuccessStatusCode)
            throw new Exception($"LibreTranslate returned {response.StatusCode} while listing languages.");

        var json = await response.Content.ReadAsStringAsync();
        _languagesCache = JsonSerializer.Deserialize<List<LanguageInfo>>(json, _jsonOptions) ?? new List<LanguageInfo>();
        _languagesCachedAt = DateTime.UtcNow;
        return _languagesCache;
    }

    private async Task<string> CallLibreTranslateAsync(string html, string source, string target)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(new { q = html, source, target, format = "html" }),
            Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/translate", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"LibreTranslate returned {response.StatusCode}: {responseBody}");

        using var doc = JsonDocument.Parse(responseBody);
        if (!doc.RootElement.TryGetProperty("translatedText", out var translatedText))
            throw new Exception("LibreTranslate response does not contain 'translatedText'.");

        return translatedText.GetString() ?? string.Empty;
    }
}