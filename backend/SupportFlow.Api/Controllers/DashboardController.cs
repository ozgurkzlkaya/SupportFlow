using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportFlow.Api.DTOs.Dashboard;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Services;

namespace SupportFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = nameof(UserRole.Admin) + "," + nameof(UserRole.TechnicalStaff))]
public sealed class DashboardController(DashboardService dashboardService) : ControllerBase
{
    [HttpGet("queue-aging")]
    [ProducesResponseType<QueueAgingDashboardResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<QueueAgingDashboardResponse>> GetQueueAging(
        CancellationToken cancellationToken) =>
        Ok(await dashboardService.GetQueueAgingAsync(cancellationToken));
}
