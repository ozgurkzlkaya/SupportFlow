using Microsoft.Extensions.Options;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Settings;

namespace SupportFlow.Api.Services.Sla;

public sealed class SlaTargetResolver(IOptions<SlaSettings> settings) : ISlaTargetResolver
{
    private readonly SlaSettings _settings = settings.Value;

    public SlaTargets GetTargets(TicketPriority priority)
    {
        var targets = priority switch
        {
            TicketPriority.Low => _settings.Low,
            TicketPriority.Normal => _settings.Normal,
            TicketPriority.High => _settings.High,
            TicketPriority.Critical => _settings.Critical,
            _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unsupported ticket priority.")
        };

        return new SlaTargets(
            TimeSpan.FromMinutes(targets.FirstResponseMinutes),
            TimeSpan.FromMinutes(targets.ResolutionMinutes));
    }
}
