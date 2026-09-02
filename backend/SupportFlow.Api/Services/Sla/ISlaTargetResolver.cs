using SupportFlow.Api.Enums;

namespace SupportFlow.Api.Services.Sla;

public interface ISlaTargetResolver
{
    SlaTargets GetTargets(TicketPriority priority);
}
