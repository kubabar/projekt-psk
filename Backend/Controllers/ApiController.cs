using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Backend.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ApiController : ControllerBase
{
    private readonly IBirApiQueueService _birApiQueue;
    private readonly INbpApiQueueService _nbpApiQueue;
    private readonly ILogger<ApiController> _logger;

    public ApiController(
        IBirApiQueueService birApiQueue,
        INbpApiQueueService nbpApiQueue,
        ILogger<ApiController> logger)
    {
        _birApiQueue = birApiQueue;
        _nbpApiQueue = nbpApiQueue;
        _logger = logger;
    }

    /// <summary>
    /// Get company information by NIP (queued)
    /// </summary>
    [HttpPost("company-info")]
    public IActionResult GetCompanyInfo([FromBody] GetCompanyInfoRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            return Unauthorized(ApiResponse<object>.ErrorResponse("Nieautoryzowany dostęp"));
        }

        try
        {
            var taskId = _birApiQueue.EnqueueTask(request.Nip, userIdClaim);

            var response = new TaskAcceptedResponse
            {
                TaskId = taskId,
                Message = "Zapytanie zostało przyjęte do realizacji. Odpowiedź zostanie dostarczona przez websocket/polling."
            };

            return Accepted(ApiResponse<TaskAcceptedResponse>.SuccessResponse(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueueing BIR API task");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("Błąd podczas przyjmowania zapytania"));
        }
    }

    /// <summary>
    /// Get currency exchange rate (queued)
    /// </summary>
    [HttpPost("currency-rate")]
    public IActionResult GetCurrencyRate([FromBody] GetCurrencyRateRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            return Unauthorized(ApiResponse<object>.ErrorResponse("Nieautoryzowany dostęp"));
        }

        try
        {
            var taskId = _nbpApiQueue.EnqueueTask(request.CurrencyCode, userIdClaim);

            var response = new TaskAcceptedResponse
            {
                TaskId = taskId,
                Message = "Zapytanie zostało przyjęte do realizacji. Odpowiedź zostanie dostarczona przez websocket/polling."
            };

            return Accepted(ApiResponse<TaskAcceptedResponse>.SuccessResponse(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueueing NBP API task");
            return StatusCode(500, ApiResponse<object>.ErrorResponse("Błąd podczas przyjmowania zapytania"));
        }
    }
}
