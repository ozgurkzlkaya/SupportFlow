using SupportFlow.Api.Services.Gmail.Models;

namespace SupportFlow.Api.Services.Gmail;

public interface IInboundEmailCorrelationService
{
    Task<InboundEmailCorrelationResult> ResolveAsync(
        InboundEmailMessage message,
        CancellationToken cancellationToken = default);
}
