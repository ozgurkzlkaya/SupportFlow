using SupportFlow.Api.Services.Gmail.Models;

namespace SupportFlow.Api.Services.Gmail;

public interface IInboundEmailProcessor
{
    Task<InboundEmailProcessingResult> ProcessAsync(
        InboundEmailMessage message,
        CancellationToken cancellationToken = default);
}
