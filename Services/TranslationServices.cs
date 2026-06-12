using System.Text;
using System.Text.Json;
using TranslatorApi.Models;

namespace TranslatorApi.Services;

public class TranslationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TranslationService> _logger;
    private readonly string _outputFilePath;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TranslationService(HttpClient httpClient, IConfiguration config, ILogger<TranslationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _outputFilePath = config["Storage:FilePath"] ?? "translations_history.json";
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


    public async Task<int> PublishAsync(TranslationRecord record)
    {
        var history = await LoadHistoryAsync();
        history.Add(record);
        await SaveHistoryAsync(history);
        _logger.LogInformation("Publicado em '{path}' ({count} registo(s)).", _outputFilePath, history.Count);
        return history.Count;
    }


    public async Task<List<TranslationRecord>> GetHistoryAsync()
    {
        return await LoadHistoryAsync();
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


    private async Task<List<TranslationRecord>> LoadHistoryAsync()
    {
        if (!File.Exists(_outputFilePath))
            return new List<TranslationRecord>();

        try
        {
            var json = await File.ReadAllTextAsync(_outputFilePath, Encoding.UTF8);
            return JsonSerializer.Deserialize<List<TranslationRecord>>(json, _jsonOptions) ?? new();
        }
        catch
        {
            _logger.LogWarning("Ficheiro JSON corrompido — a começar histórico novo.");
            return new List<TranslationRecord>();
        }
    }


    private async Task SaveHistoryAsync(List<TranslationRecord> history)
    {
        await File.WriteAllTextAsync(_outputFilePath, JsonSerializer.Serialize(history, _jsonOptions), Encoding.UTF8);
    }
}