using SupportFlow.Api.DTOs.Dashboard;

namespace SupportFlow.Api.Services;

public interface IAgentWorkloadService
{
    Task<AgentWorkloadResponse> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
