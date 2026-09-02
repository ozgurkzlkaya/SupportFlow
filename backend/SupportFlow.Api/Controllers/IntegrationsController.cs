using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using SupportFlow.Api.DTOs.Gmail;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Services.Gmail;
using SupportFlow.Api.Services.Gmail.Models;
using SupportFlow.Api.Settings;

namespace SupportFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class IntegrationsController : ControllerBase
{
    private readonly IGmailAuthorizationService _gmailAuthorizationService;
    private readonly IGmailMailboxProcessingCoordinator _gmailProcessingCoordinator;
    private readonly GmailDiagnosticsService _gmailDiagnosticsService;
    private readonly GmailBackgroundSyncRuntimeStatus _backgroundSyncRuntimeStatus;
    private readonly GmailBackgroundSyncSettings _backgroundSyncSettings;

    public IntegrationsController(
        IGmailAuthorizationService gmailAuthorizationService,
        IGmailMailboxProcessingCoordinator gmailProcessingCoordinator,
        GmailDiagnosticsService gmailDiagnosticsService,
        GmailBackgroundSyncRuntimeStatus backgroundSyncRuntimeStatus,
        IOptions<GmailBackgroundSyncSettings> backgroundSyncSettings)
    {
        _gmailAuthorizationService = gmailAuthorizationService;
        _gmailProcessingCoordinator = gmailProcessingCoordinator;
        _gmailDiagnosticsService = gmailDiagnosticsService;
        _backgroundSyncRuntimeStatus = backgroundSyncRuntimeStatus;
        _backgroundSyncSettings = backgroundSyncSettings.Value;
    }

    [HttpGet("gmail/oauth/start")]
    [ProducesResponseType<GmailOAuthStartResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GmailOAuthStartResponse>> StartGmailOAuth(
        [FromQuery] string? returnTarget,
        CancellationToken cancellationToken)
    {
        if (!TryParseReturnTarget(returnTarget, out var parsedReturnTarget))
        {
            return BadRequest(new { message = "The OAuth return target is invalid." });
        }

        try
        {
            var response = await _gmailAuthorizationService.CreateAuthorizationStartAsync(
                parsedReturnTarget,
                cancellationToken);
            return Ok(response);
        }
        catch (GmailIntegrationUnavailableException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Gmail integration is unavailable.",
                detail: exception.Message);
        }
    }

    [AllowAnonymous]
    [HttpGet("gmail/oauth/callback")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GmailOAuthCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _gmailAuthorizationService.CompleteAuthorizationAsync(
                code,
                state,
                error,
                cancellationToken);

            return result.ReturnTarget == GmailOAuthReturnTarget.Mobile
                ? RedirectToMobileAuthorizationResult("success")
                : Ok(new { message = result.Message });
        }
        catch (GmailIntegrationUnavailableException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Gmail integration is unavailable.",
                detail: exception.Message);
        }
        catch (GmailOAuthCompletionException exception)
        {
            return exception.ReturnTarget == GmailOAuthReturnTarget.Mobile
                ? RedirectToMobileAuthorizationResult("error")
                : BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private IActionResult RedirectToMobileAuthorizationResult(string status) =>
        Redirect(QueryHelpers.AddQueryString(
            _gmailAuthorizationService.GetMobileReturnUri(),
            "status",
            status));

    private static bool TryParseReturnTarget(
        string? value,
        out GmailOAuthReturnTarget returnTarget)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            returnTarget = GmailOAuthReturnTarget.Web;
            return true;
        }

        if (string.Equals(value, "mobile", StringComparison.OrdinalIgnoreCase))
        {
            returnTarget = GmailOAuthReturnTarget.Mobile;
            return true;
        }

        returnTarget = default;
        return false;
    }

    [HttpGet("gmail/oauth/status")]
    [ProducesResponseType<GmailOAuthStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GmailOAuthStatusResponse>> GetGmailOAuthStatus(
        CancellationToken cancellationToken)
    {
        var response = await _gmailAuthorizationService.GetStatusAsync(cancellationToken);
        response.BackgroundSync = _backgroundSyncRuntimeStatus.GetSnapshot(
            _backgroundSyncSettings.Enabled,
            _backgroundSyncSettings.IntervalSeconds);
        return Ok(response);
    }

    [HttpPost("gmail/process")]
    [ProducesResponseType<GmailMailboxProcessingSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType<GmailRateLimitResponse>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GmailMailboxProcessingSummary>> ProcessGmail(
        CancellationToken cancellationToken)
    {
        try
        {
            var summary = await _gmailProcessingCoordinator.ProcessAsync(cancellationToken);
            return Ok(summary);
        }
        catch (GmailAuthorizationRequiredException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (GmailRateLimitedException exception)
        {
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                CreateRateLimitResponse(exception));
        }
        catch (GmailIntegrationUnavailableException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Gmail integration is unavailable.",
                detail: exception.Message);
        }
    }

    [HttpGet("gmail/diagnostics/messages")]
    [ProducesResponseType<IReadOnlyList<GmailMessageDiagnosticResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<GmailRateLimitResponse>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<GmailMessageDiagnosticResponse>>> GetGmailDiagnostics(
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _gmailDiagnosticsService.GetRecentMessagesAsync(cancellationToken));
        }
        catch (GmailAuthorizationRequiredException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (GmailRateLimitedException exception)
        {
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                CreateRateLimitResponse(exception));
        }
        catch (GmailIntegrationUnavailableException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Gmail integration is unavailable.",
                detail: exception.Message);
        }
    }

    private static GmailRateLimitResponse CreateRateLimitResponse(
        GmailRateLimitedException exception) =>
        new(
            "RateLimited",
            "Gmail API geçici olarak hız sınırında. Yeniden deneme zamanı bilgisi kullanılabilir.",
            exception.RetryAtUtc);

}
