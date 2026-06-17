namespace TranslatorApi.Models;

public class TranslationRequest
{
    public string HtmlContent { get; set; } = string.Empty;
    public string SourceLanguage { get; set; } = "pt";
    public List<string> TargetLanguages { get; set; } = new();
}

public class TranslationResult
{
    public string Language { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;
}

public class TranslationRecord
{
    public DateTime RequestDate { get; set; } = DateTime.UtcNow;
    public string SourceLanguage { get; set; } = string.Empty;
    public string OriginalText { get; set; } = string.Empty;
    public List<TranslationResult> Translations { get; set; } = new();
}

public class TranslationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public TranslationRecord? Result { get; set; }
}

public class PublishRequest
{
    public TranslationRecord Record { get; set; } = new();
}

public class PublishResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int TotalRecords { get; set; }
}

public class HistoricoEntry
{
    public string FileName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public long SizeBytes { get; set; }
}