using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SupportFlow.Api.DTOs.Attachments;
using SupportFlow.Api.DTOs.Common;
using SupportFlow.Api.DTOs.Tickets;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Services;

namespace SupportFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly TicketService _ticketService;
    private readonly AttachmentService _attachmentService;
    private readonly TicketTimelineService _ticketTimelineService;

    public TicketsController(
        TicketService ticketService,
        AttachmentService attachmentService,
        TicketTimelineService ticketTimelineService)
    {
        _ticketService = ticketService;
        _attachmentService = attachmentService;
        _ticketTimelineService = ticketTimelineService;
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Customer))]
    [ProducesResponseType<TicketDetailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TicketDetailResponse>> Create(
        CreateTicketRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentIdentity(out var userId, out _))
        {
            return Unauthorized();
        }

        var ticket = await _ticketService.CreateAsync(userId, request, cancellationToken);

        return ticket is null
            ? Unauthorized()
            : CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ticket);
    }

    [HttpGet]
    [ProducesResponseType<PagedResponse<TicketListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResponse<TicketListItemResponse>>> GetAll(
        [FromQuery] TicketPriority? priority,
        [FromQuery] TicketStatus? status,
        [FromQuery] int? assignedUserId,
        [FromQuery] TicketAssignmentFilter? assignmentState,
        [FromQuery] bool activeOnly,
        [FromQuery] string? search,
        [FromQuery] TicketAgingFilter? agingFilter,
        [FromQuery] TicketListSort? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default,
        [FromQuery] int? categoryId = null,
        [FromQuery] TicketClassificationFilter? classificationState = null,
        [FromQuery] int? tagId = null)
    {
        if (!TryGetCurrentIdentity(out var userId, out var role))
        {
            return Unauthorized();
        }

        if (priority.HasValue && !Enum.IsDefined(priority.Value))
        {
            return BadRequest(new { message = "The supplied ticket priority is invalid." });
        }

        if (status.HasValue && !Enum.IsDefined(status.Value))
        {
            return BadRequest(new { message = "The supplied ticket status is invalid." });
        }

        if (agingFilter.HasValue && !Enum.IsDefined(agingFilter.Value))
        {
            return BadRequest(new { message = "The supplied aging filter is invalid." });
        }

        if (sort.HasValue && !Enum.IsDefined(sort.Value))
        {
            return BadRequest(new { message = "The supplied ticket sort is invalid." });
        }

        if (assignedUserId.HasValue && assignedUserId.Value <= 0)
        {
            return BadRequest(new { message = "The supplied assigned user id is invalid." });
        }

        if (categoryId.HasValue && categoryId.Value <= 0)
        {
            return BadRequest(new { message = "The supplied category id is invalid." });
        }

        if (tagId.HasValue && tagId.Value <= 0)
        {
            return BadRequest(new { message = "The supplied tag id is invalid." });
        }

        if (classificationState.HasValue && !Enum.IsDefined(classificationState.Value))
        {
            return BadRequest(new { message = "The supplied classification filter is invalid." });
        }

        if (categoryId.HasValue && classificationState.HasValue)
        {
            return BadRequest(new { message = "Category id and classification state cannot be combined." });
        }

        if (page <= 0)
        {
            return BadRequest(new { message = "The supplied page is invalid." });
        }

        if (pageSize <= 0 || pageSize > 100)
        {
            return BadRequest(new { message = "The supplied page size is invalid." });
        }

        if (assignmentState.HasValue && !Enum.IsDefined(assignmentState.Value))
        {
            return BadRequest(new { message = "The supplied assignment filter is invalid." });
        }

        if (assignedUserId.HasValue && assignmentState.HasValue)
        {
            return BadRequest(new { message = "Assigned user and assignment state cannot be combined." });
        }

        if (assignmentState == TicketAssignmentFilter.InactiveAssignee
            && role != UserRole.Admin)
        {
            return Forbid();
        }

        var tickets = await _ticketService.GetAllAsync(
            userId,
            role,
            priority,
            status,
            assignedUserId,
            assignmentState,
            activeOnly,
            search,
            agingFilter,
            sort,
            categoryId,
            classificationState,
            tagId,
            page,
            pageSize,
            cancellationToken);
        return Ok(tickets);
    }

    [HttpPatch("{id:int}/classification")]
    [Authorize(Roles = nameof(UserRole.TechnicalStaff) + "," + nameof(UserRole.Admin))]
    [ProducesResponseType<TicketClassificationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketClassificationResponse>> UpdateClassification(
        int id,
        UpdateTicketClassificationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentIdentity(out var userId, out var role))
        {
            return Unauthorized();
        }

        var result = await _ticketService.UpdateClassificationAsync(
            id,
            userId,
            role,
            request,
            cancellationToken);

        return result.Failure == TicketOperationFailure.None
            ? Ok(result.Value)
            : GetFailureResult(result.Failure, result.Error);
    }

    [HttpGet("summary")]
    [ProducesResponseType<TicketDashboardSummaryResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TicketDashboardSummaryResponse>> GetSummary(
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentIdentity(out var userId, out var role))
        {
            return Unauthorized();
        }

        return Ok(await _ticketService.GetDashboardSummaryAsync(userId, role, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<TicketDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDetailResponse>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentIdentity(out var userId, out var role))
        {
            return Unauthorized();
        }

        var ticket = await _ticketService.GetByIdAsync(id, userId, role, cancellationToken);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    [HttpGet("{id:int}/timeline")]
    [ProducesResponseType<PagedResponse<TicketTimelineItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<TicketTimelineItemResponse>>> GetTimeline(
        int id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentIdentity(out var userId, out var role))
        {
            return Unauthorized();
        }

        if (page <= 0)
        {
            return BadRequest(new { message = "The supplied page is invalid." });
        }

        if (pageSize <= 0 || pageSize > 100)
        {
            return BadRequest(new { message = "The supplied page size is invalid." });
        }

        var result = await _ticketTimelineService.GetTimelineAsync(
            id,
            userId,
            role,
            page,
            pageSize,
            cancellationToken);

        return result.Failure == TicketOperationFailure.None
            ? Ok(result.Value)
            : GetFailureResult(result.Failure, result.Error);
    }

    [HttpGet("{id:int}/messages")]
    [ProducesResponseType<IReadOnlyList<TicketMessageResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TicketMessageResponse>>> GetMessages(
        int id,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentIdentity(out var userId, out var role))
        {
            return Unauthorized();
        }

        var result = await _ticketService.GetMessagesAsync(
            id,
            userId,
            role,
            cancellationToken);

        return result.Failure == TicketOperationFailure.None
            ? Ok(result.Value)
            : GetFailureResult(result.Failure, result.Error);
    }

    [HttpPost("{id:int}/messages")]
    [ProducesResponseType<TicketMessageResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketMessageResponse>> AddMessage(
        int id,
        CreateTicketMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentIdentity(out var userId, out var role))
        {
            return Unauthorized();
        }

        var result = await _ticketService.AddMessageAsync(
            id,
            userId,
            role,
            request,
            cancellationToken);

        if (result.Failure == TicketOperationFailure.None)
        {
            return CreatedAtAction(nameof(GetMessages), new { id }, result.Value);
        }

        if (result.Failure == TicketOperationFailure.DeliveryFailed)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                deliveryFailed = true,
                message = result.Error,
                ticketMessage = result.Value
            });
        }

        return GetFailureResult(result.Failure, result.Error);
    }

    [HttpPost("{id:int}/messages/{messageId:int}/retry-delivery")]
    [Authorize(Roles = nameof(UserRole.TechnicalStaff) + "," + nameof(UserRole.Admin))]
    [ProducesResponseType<TicketMessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketMessageResponse>> RetryMessageDelivery(
        int id,
        int messageId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentIdentity(out var userId, out var role))
        {
            return Unauthorized();
        }

        var result = await _ticketService.RetryMessageDeliveryAsync(
            id,
            messageId,
            userId,
            role,
            cancellationToken);

        if (result.Failure == TicketOperationFailure.None)
        {
            return Ok(result.Value);
        }

        if (result.Failure == TicketOperationFailure.DeliveryFailed)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                deliveryFailed = true,
                message = result.Error,
                ticketMessage = result.Value
            });
        }

        return GetFailureResult(result.Failure, result.Error);
    }

    [HttpPatch("{id:int}/assign")]
    [Authorize(Roles = nameof(UserRole.TechnicalStaff) + "," + nameof(UserRole.Admin))]
    [ProducesResponseType<TicketDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDetailResponse>> Assign(
        int id,
        AssignTicketRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentIdentity(out var userId, out var role))
        {
            return Unauthorized();
        }

        var result = await _ticketService.AssignAsync(
            id,
            userId,
            role,
            request,
            cancellationToken);

        return result.Failure == TicketOperationFailure.None
            ? Ok(result.Value)
            : GetFailureResult(result.Failure, result.Error);
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = nameof(UserRole.TechnicalStaff) + "," + nameof(UserRole.Admin))]
    [ProducesResponseType<TicketDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDetailResponse>> UpdateStatus(
        int id,
        UpdateTicketStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentIdentity(out var userId, out var role))
        {
            return Unauthorized();
        }

        var result = await _ticketService.UpdateStatusAsync(
            id,
            userId,
            role,
            request,
            cancellationToken);

        return result.Failure == TicketOperationFailure.None
            ? Ok(result.Value)
            : GetFailureResult(result.Failure, result.Error);
    }

    [HttpPatch("{id:int}/priority")]
    [Authorize(Roles = nameof(UserRole.TechnicalStaff) + "," + nameof(UserRole.Admin))]
    [ProducesResponseType<TicketDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDetailResponse>> UpdatePriority(
        int id,
        UpdateTicketPriorityRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentIdentity(out var userId, out var role))
        {
            return Unauthorized();
        }

        var result = await _ticketService.UpdatePriorityAsync(
            id,
            userId,
            role,
            request,
            cancellationToken);

        return result.Failure == TicketOperationFailure.None
            ? Ok(result.Value)
            : GetFailureResult(result.Failure, result.Error);
    }

    [HttpGet("{id:int}/history")]
    [ProducesResponseType<IReadOnlyList<TicketStatusHistoryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TicketStatusHistoryResponse>>> GetHistory(
        int id,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentIdentity(out var userId, out var role))
        {
            return Unauthorized();
        }

        var result = await _ticketService.GetHistoryAsync(
            id,
            userId,
            role,
            cancellationToken);

        return result.Failure == TicketOperationFailure.None
            ? Ok(result.Value)
            : GetFailureResult(result.Failure, result.Error);
    }

    [HttpGet("{id:int}/attachments")]
    [ProducesResponseType<IReadOnlyList<TicketAttachmentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TicketAttachmentResponse>>> GetAttachments(
        int id,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentIdentity(out var userId, out var role))
        {
            return Unauthorized();
        }

        var result = await _attachmentService.GetAttachmentsAsync(
            id,
            userId,
            role,
            cancellationToken);

        return result.Failure == TicketOperationFailure.None
            ? Ok(result.Value)
            : GetFailureResult(result.Failure, result.Error);
    }

    [HttpPost("{id:int}/attachments")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<TicketAttachmentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketAttachmentResponse>> UploadAttachment(
        int id,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentIdentity(out var userId, out var role))
        {
            return Unauthorized();
        }

        var result = await _attachmentService.UploadAttachmentAsync(
            id,
            userId,
            role,
            file,
            cancellationToken);

        return result.Failure == TicketOperationFailure.None
            ? CreatedAtAction(
                nameof(DownloadAttachment),
                new { id, attachmentId = result.Value!.Id },
                result.Value)
            : GetFailureResult(result.Failure, result.Error);
    }

    [HttpGet("{id:int}/attachments/{attachmentId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DownloadAttachment(
        int id,
        int attachmentId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentIdentity(out var userId, out var role))
        {
            return Unauthorized();
        }

        var result = await _attachmentService.GetAttachmentFileAsync(
            id,
            attachmentId,
            userId,
            role,
            cancellationToken);

        if (result.Failure != TicketOperationFailure.None || result.Value is null)
        {
            return GetFailureResult(result.Failure, result.Error);
        }

        return File(
            result.Value.Stream,
            result.Value.ContentType,
            result.Value.FileName,
            enableRangeProcessing: true);
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

    private ActionResult GetFailureResult(TicketOperationFailure failure, string? error)
    {
        return failure switch
        {
            TicketOperationFailure.NotFound => NotFound(),
            TicketOperationFailure.InvalidRequest => BadRequest(new { message = error }),
            TicketOperationFailure.Forbidden => Forbid(),
            TicketOperationFailure.Unauthorized => Unauthorized(),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
