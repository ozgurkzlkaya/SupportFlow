using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportFlow.Api.DTOs.AdminUsers;
using SupportFlow.Api.DTOs.Common;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Services;

namespace SupportFlow.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminUsersController(AdminUserManagementService adminUserManagementService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<AdminUserListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<AdminUserListItemResponse>>> GetUsers(
        [FromQuery] string? search,
        [FromQuery] UserRole? role,
        [FromQuery] UserAccountStatusFilter? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (role.HasValue && !Enum.IsDefined(role.Value))
        {
            return BadRequest(new { message = "The supplied user role is invalid." });
        }

        if (status.HasValue && !Enum.IsDefined(status.Value))
        {
            return BadRequest(new { message = "The supplied user status is invalid." });
        }

        if (page <= 0)
        {
            return BadRequest(new { message = "The supplied page is invalid." });
        }

        if (pageSize <= 0 || pageSize > 100)
        {
            return BadRequest(new { message = "The supplied page size is invalid." });
        }

        return Ok(await adminUserManagementService.GetUsersAsync(
            search,
            role,
            status,
            page,
            pageSize,
            cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<AdminUserDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminUserDetailResponse>> GetUser(
        int id,
        CancellationToken cancellationToken)
    {
        var user = await adminUserManagementService.GetUserAsync(id, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpGet("{id:int}/audit")]
    [ProducesResponseType<PagedResponse<AdminUserAuditResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<AdminUserAuditResponse>>> GetUserAudits(
        int id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (page <= 0)
        {
            return BadRequest(new { message = "The supplied page is invalid." });
        }

        if (pageSize <= 0 || pageSize > 100)
        {
            return BadRequest(new { message = "The supplied page size is invalid." });
        }

        var result = await adminUserManagementService.GetUserAuditsAsync(id, page, pageSize, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPatch("{id:int}/status")]
    [ProducesResponseType<AdminUserStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminUserStatusResponse>> UpdateStatus(
        int id,
        UpdateUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await adminUserManagementService.UpdateStatusAsync(
            actorUserId,
            id,
            request.IsActive,
            cancellationToken);

        return result.Failure switch
        {
            AdminUserStatusUpdateFailure.None => Ok(result.User),
            AdminUserStatusUpdateFailure.ActorNotFound => Unauthorized(),
            AdminUserStatusUpdateFailure.NotFound => NotFound(),
            AdminUserStatusUpdateFailure.SelfDeactivation => BadRequest(new { message = "You cannot deactivate your own account." }),
            AdminUserStatusUpdateFailure.LastActiveAdmin => BadRequest(new { message = "At least one active administrator is required." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
