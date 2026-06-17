using System.Text;
using System.Text.Json;
using TranslatorApi.Models;
using Microsoft.Extensions.Hosting;

namespace TranslatorApi.Services;

public class TranslationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TranslationService> _logger;
    private readonly string _historicoFolder;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TranslationService(HttpClient httpClient, IConfiguration config, IHostEnvironment env, ILogger<TranslationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _historicoFolder = ResolveFolder(config["Storage:HistoricoFolder"], env.ContentRootPath);
    }

    private static string ResolveFolder(string? configured, string contentRootPath)
    {
        // Se vier um caminho absoluto na config, usa-o directamente
        if (!string.IsNullOrWhiteSpace(configured) && Path.IsPathRooted(configured))
        {
            Directory.CreateDirectory(configured);
            return configured;
        }

        // Por defeito, guarda numa pasta "historico" na raiz do projeto
        // (ContentRootPath = pasta onde está o .csproj quando corres com "dotnet run" / VS Code)
        var subFolder = !string.IsNullOrWhiteSpace(configured)
            ? configured                        // nome/caminho relativo vindo da config
            : "historico";

        var folder = Path.Combine(contentRootPath, subFolder);
        Directory.CreateDirectory(folder);
        return folder;
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
            _logger.LogInformation("A traduzir para [{lang}]...", targetLang);
            try
            {
                var translated = await CallLibreTranslateAsync(request.HtmlContent, request.SourceLanguage, targetLang);
                record.Translations.Add(new TranslationResult
                {
                    Language = targetLang,
                    TranslatedText = translated
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao traduzir para {lang}", targetLang);
                record.Translations.Add(new TranslationResult
                {
                    Language = targetLang,
                    TranslatedText = $"[ERRO: {ex.Message}]"
                });
            }
        }

        return record;
    }

    public async Task<string> PublishAsync(TranslationRecord record)
    {
        var langs = string.Join("-", record.Translations.Select(t => t.Language));
        var timestamp = record.RequestDate.ToString("yyyyMMdd_HHmmss");
        var fileName = $"traducao_{timestamp}_{record.SourceLanguage}-{langs}.json";
        var filePath = Path.Combine(_historicoFolder, fileName);

        var json = JsonSerializer.Serialize(record, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);

        _logger.LogInformation("Guardado: '{path}'", filePath);
        return fileName;
    }

    public List<HistoricoEntry> ListHistorico()
    {
        return Directory
            .EnumerateFiles(_historicoFolder, "traducao_*.json")
            .Select(path => new FileInfo(path))
            .OrderByDescending(fi => fi.LastWriteTimeUtc)
            .Select(fi => new HistoricoEntry
            {
                FileName = fi.Name,
                CreatedAt = fi.LastWriteTimeUtc,
                SizeBytes = fi.Length
            })
            .ToList();
    }


    public async Task<(byte[] bytes, string fileName)?> DownloadAsync(string fileName)
    {
        var safe = Path.GetFileName(fileName);
        if (!safe.StartsWith("traducao_") || !safe.EndsWith(".json"))
            return null;

        var path = Path.Combine(_historicoFolder, safe);
        if (!File.Exists(path))
            return null;

        var bytes = await File.ReadAllBytesAsync(path);
        return (bytes, safe);
    }


    public async Task<TranslationRecord?> GetRecordAsync(string fileName)
    {
        var safe = Path.GetFileName(fileName);
        if (!safe.StartsWith("traducao_") || !safe.EndsWith(".json"))
            return null;

        var path = Path.Combine(_historicoFolder, safe);
        if (!File.Exists(path))
            return null;

        var json = await File.ReadAllTextAsync(path, Encoding.UTF8);
        return JsonSerializer.Deserialize<TranslationRecord>(json, _jsonOptions);
    }


    private async Task<string> CallLibreTranslateAsync(string html, string source, string target)
    {
        var requestBody = new { q = html, source, target, format = "html" };
        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/translate", content);

        if (!response.IsSuccessStatusCode)
            throw new Exception($"LibreTranslate devolveu {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        if (!doc.RootElement.TryGetProperty("translatedText", out var translatedText))
            throw new Exception("Resposta do LibreTranslate não contém 'translatedText'.");

        return translatedText.GetString() ?? string.Empty;
    }
}