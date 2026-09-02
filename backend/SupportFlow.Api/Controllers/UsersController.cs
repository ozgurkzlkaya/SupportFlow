using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportFlow.Api.DTOs.Users;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Services;

namespace SupportFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;

    public UsersController(UserService userService)
    {
        _userService = userService;
    }

    [HttpGet("me")]
    [ProducesResponseType<UserProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileResponse>> GetProfile(
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var profile = await _userService.GetProfileAsync(userId, cancellationToken);
        return profile is null ? Unauthorized() : Ok(profile);
    }

    [HttpPatch("me")]
    [ProducesResponseType<UserProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileResponse>> UpdateProfile(
        UpdateUserProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _userService.UpdateProfileAsync(userId, request, cancellationToken);

        if (!result.Found)
        {
            return Unauthorized();
        }

        return result.Error is null
            ? Ok(result.Profile)
            : BadRequest(new { message = result.Error });
    }

    [HttpPost("me/change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _userService.ChangePasswordAsync(userId, request, cancellationToken);

        if (!result.Found)
        {
            return Unauthorized();
        }

        return result.Error is null
            ? NoContent()
            : BadRequest(new { message = result.Error });
    }

    [HttpGet("technical-staff")]
    [Authorize(Roles = nameof(UserRole.TechnicalStaff) + "," + nameof(UserRole.Admin))]
    [ProducesResponseType<IReadOnlyList<TechnicalStaffResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<TechnicalStaffResponse>>> GetTechnicalStaff(
        CancellationToken cancellationToken)
    {
        var users = await _userService.GetActiveTechnicalStaffAsync(cancellationToken);
        return Ok(users);
    }

    private bool TryGetCurrentUserId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
