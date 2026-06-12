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
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public TranslationService(HttpClient httpClient, IConfiguration config, ILogger<TranslationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _outputFilePath = config["Storage:FilePath"] ?? "traducoes_historico.json";
    }

    public async Task<TranslationRecord> TranslateAsync(TranslationRequest request)
    {
        var record = new TranslationRecord
        {
            DataPedido = DateTime.Now,
            IdiomaOrigem = request.SourceLanguage,
            TextoOriginal = request.HtmlContent
        };

        foreach (var targetLang in request.TargetLanguages)
        {
            _logger.LogInformation("A traduzir para [{lang}]...", targetLang);
            try
            {
                var translated = await CallLibreTranslateAsync(request.HtmlContent, request.SourceLanguage, targetLang);
                record.Traducoes.Add(new TranslationResult { Idioma = targetLang, TextoTraduzido = translated });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao traduzir para {lang}", targetLang);
                record.Traducoes.Add(new TranslationResult { Idioma = targetLang, TextoTraduzido = $"[ERRO: {ex.Message}]" });
            }
        }

        await SaveToJsonFileAsync(record);
        return record;
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

    private async Task SaveToJsonFileAsync(TranslationRecord record)
    {
        List<TranslationRecord> history = new();

        if (File.Exists(_outputFilePath))
        {
            try
            {
                var existing = await File.ReadAllTextAsync(_outputFilePath, Encoding.UTF8);
                history = JsonSerializer.Deserialize<List<TranslationRecord>>(existing, _jsonOptions) ?? new();
            }
            catch { _logger.LogWarning("Ficheiro JSON corrompido — a criar novo."); }
        }

        history.Add(record);
        await File.WriteAllTextAsync(_outputFilePath, JsonSerializer.Serialize(history, _jsonOptions), Encoding.UTF8);
        _logger.LogInformation("Guardado em '{path}' ({count} registo(s)).", _outputFilePath, history.Count);
    }
}