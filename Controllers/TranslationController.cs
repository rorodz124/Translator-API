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
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Translate([FromBody] TranslationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.HtmlContent))
            return BadRequest(new { error = "The 'htmlContent' field cannot be empty." });

        if (request.TargetLanguages == null || request.TargetLanguages.Count == 0)
            return BadRequest(new { error = "Please select at least one target language." });

        _logger.LogInformation("Translation request: source={src}, targets={targets}",
            request.SourceLanguage, string.Join(", ", request.TargetLanguages));

        try
        {
            var record = await _translationService.TranslateAsync(request);
            return Ok(new TranslationResponse
            {
                Success = true,
                Message = $"{record.Translations.Count} translation(s) completed and saved.",
                Result = record
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during translation.");
            return StatusCode(502, new { error = ex.Message });
        }
    }

    [HttpGet("history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetHistory([FromServices] IConfiguration config)
    {
        var filePath = config["Storage:FilePath"] ?? "translations_history.json";

        if (!System.IO.File.Exists(filePath))
            return Ok(new { message = "No translations have been saved yet." });

        return Content(System.IO.File.ReadAllText(filePath), "application/json");
    }
}