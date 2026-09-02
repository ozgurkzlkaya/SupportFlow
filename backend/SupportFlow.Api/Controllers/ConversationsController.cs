using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportFlow.Api.DTOs.Conversations;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Services;

namespace SupportFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = nameof(UserRole.Admin) + "," + nameof(UserRole.TechnicalStaff))]
public sealed class ConversationsController : ControllerBase
{
    private readonly ConversationService _conversationService;

    public ConversationsController(ConversationService conversationService)
    {
        _conversationService = conversationService;
    }

    [HttpGet]
    [ProducesResponseType<ConversationSummaryListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConversationSummaryListResponse>> GetSummaries(
        [FromQuery] string? search,
        [FromQuery] ConversationFilter filter,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(filter))
        {
            return BadRequest();
        }

        if (!TryGetCurrentIdentity(out var userId, out _))
        {
            return Unauthorized();
        }

        var conversations = await _conversationService.GetSummaryListAsync(
            userId,
            search,
            filter,
            cancellationToken);
        return Ok(conversations);
    }

    [HttpPost("{requesterType}/{requesterId:int}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(
        ConversationRequesterType requesterType,
        int requesterId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentIdentity(out var userId, out var role))
        {
            return Unauthorized();
        }

        var result = await _conversationService.MarkReadAsync(
            userId,
            role,
            requesterType,
            requesterId,
            cancellationToken);
        return GetPinFailureResult(result.Failure);
    }

    [HttpGet("{requesterType}/{requesterId:int}")]
    [ProducesResponseType<ConversationDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationDetailResponse>> GetDetail(
        ConversationRequesterType requesterType,
        int requesterId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentIdentity(out var userId, out _))
        {
            return Unauthorized();
        }

        var conversation = await _conversationService.GetDetailAsync(
            requesterType,
            requesterId,
            userId,
            cancellationToken);
        return conversation is null ? NotFound() : Ok(conversation);
    }

    [HttpPut("{requesterType}/{requesterId:int}/pin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Pin(
        ConversationRequesterType requesterType,
        int requesterId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentIdentity(out var userId, out var role))
        {
            return Unauthorized();
        }

        var result = await _conversationService.PinAsync(
            userId,
            role,
            requesterType,
            requesterId,
            cancellationToken);

        return GetPinFailureResult(result.Failure);
    }

    [HttpDelete("{requesterType}/{requesterId:int}/pin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Unpin(
        ConversationRequesterType requesterType,
        int requesterId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentIdentity(out var userId, out var role))
        {
            return Unauthorized();
        }

        var result = await _conversationService.UnpinAsync(
            userId,
            role,
            requesterType,
            requesterId,
            cancellationToken);

        return GetPinFailureResult(result.Failure);
    }

    private bool TryGetCurrentIdentity(out int userId, out UserRole role)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var roleValue = User.FindFirstValue(ClaimTypes.Role);
        role = default;

        return int.TryParse(userIdValue, out userId)
            && Enum.TryParse(roleValue, out role)
            && Enum.IsDefined(role);
    }

    private IActionResult GetPinFailureResult(ConversationPinOperationFailure failure) =>
        failure switch
        {
            ConversationPinOperationFailure.None => NoContent(),
            ConversationPinOperationFailure.InvalidRequest => BadRequest(),
            ConversationPinOperationFailure.NotFound => NotFound(),
            ConversationPinOperationFailure.Forbidden => Forbid(),
            ConversationPinOperationFailure.Unauthorized => Unauthorized(),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
}
