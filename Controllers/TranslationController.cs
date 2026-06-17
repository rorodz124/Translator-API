using Microsoft.AspNetCore.Mvc;
using TranslatorApi.Models;
using TranslatorApi.Services;

namespace TranslatorApi.Controllers;

[ApiController]
[Route("api")]
public class TranslationController : ControllerBase
{
    private readonly TranslationService _translationService;
    private readonly ILogger<TranslationController> _logger;

    public TranslationController(TranslationService translationService, ILogger<TranslationController> logger)
    {
        _translationService = translationService;
        _logger = logger;
    }


    [HttpPost("translate")]
    [ProducesResponseType(typeof(TranslationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Translate([FromBody] TranslationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.HtmlContent))
            return BadRequest(new { error = "O campo 'htmlContent' não pode estar vazio." });

        if (request.TargetLanguages == null || request.TargetLanguages.Count == 0)
            return BadRequest(new { error = "Seleciona pelo menos um idioma de destino." });

        _logger.LogInformation("Pedido de tradução: origem={src}, destinos={targets}",
            request.SourceLanguage, string.Join(", ", request.TargetLanguages));

        try
        {
            var record = await _translationService.TranslateAsync(request);
            return Ok(new TranslationResponse
            {
                Success = true,
                Message = "Tradução concluída.",
                Result = record
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado durante a tradução.");
            return StatusCode(502, new { error = ex.Message });
        }
    }


    [HttpPost("publish")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Publish([FromBody] PublishRequest request)
    {
        if (request.Record == null)
            return BadRequest(new { error = "O campo 'record' é obrigatório." });

        if (string.IsNullOrWhiteSpace(request.Record.OriginalText))
            return BadRequest(new { error = "O registo não tem texto original." });

        _logger.LogInformation("A guardar registo de tradução ({count} idioma(s))...",
            request.Record.Translations.Count);

        try
        {
            var fileName = await _translationService.PublishAsync(request.Record);
            return Ok(new
            {
                success = true,
                message = "Tradução guardada com sucesso.",
                fileName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao guardar registo.");
            return StatusCode(500, new { error = ex.Message });
        }
    }


    [HttpGet("historico")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetHistorico()
    {
        var entries = _translationService.ListHistorico();
        return Ok(new { total = entries.Count, entries });
    }


    [HttpGet("historico/{fileName}")]
    public async Task<IActionResult> DownloadHistorico(string fileName)
    {
        var result = await _translationService.DownloadAsync(fileName);
        if (result is null)
            return NotFound(new { error = "Ficheiro não encontrado ou nome inválido." });

        var (bytes, name) = result.Value;
        return File(bytes, "application/json", name);
    }


    [HttpGet("historico/{fileName}/conteudo")]
    [ProducesResponseType(typeof(TranslationRecord), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistoricoConteudo(string fileName)
    {
        var record = await _translationService.GetRecordAsync(fileName);
        if (record is null)
            return NotFound(new { error = "Ficheiro não encontrado ou nome inválido." });

        return Ok(record);
    }
}