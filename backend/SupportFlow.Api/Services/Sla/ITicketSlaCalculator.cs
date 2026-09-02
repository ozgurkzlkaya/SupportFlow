using SupportFlow.Api.DTOs.Sla;
using SupportFlow.Api.Models;

namespace SupportFlow.Api.Services.Sla;

public interface ITicketSlaCalculator
{
    TicketSlaResponse Calculate(Ticket ticket);

    TicketSlaSummaryResponse CalculateSummary(Ticket ticket);
}
