using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportFlow.Api.DTOs.Dashboard;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Services;

namespace SupportFlow.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AgentWorkloadController(IAgentWorkloadService agentWorkloadService) : ControllerBase
{
    [HttpGet("agent-workload")]
    [ProducesResponseType<AgentWorkloadResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AgentWorkloadResponse>> GetAgentWorkload(
        CancellationToken cancellationToken) =>
        Ok(await agentWorkloadService.GetSnapshotAsync(cancellationToken));
}
