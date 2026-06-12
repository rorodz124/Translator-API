namespace TranslatorApi.Models;

public class TranslationRequest
{
    public string HtmlContent { get; set; } = string.Empty;
    public string SourceLanguage { get; set; } = "pt";
    public List<string> TargetLanguages { get; set; } = new();
}

public class TranslationResult
{
    public string Idioma { get; set; } = string.Empty;
    public string TextoTraduzido { get; set; } = string.Empty;
}

public class TranslationRecord
{
    public DateTime DataPedido { get; set; } = DateTime.Now;
    public string IdiomaOrigem { get; set; } = string.Empty;
    public string TextoOriginal { get; set; } = string.Empty;
    public List<TranslationResult> Traducoes { get; set; } = new();
}

public class TranslationResponse
{
    public bool Sucesso { get; set; }
    public string Mensagem { get; set; } = string.Empty;
    public TranslationRecord? Resultado { get; set; }
}