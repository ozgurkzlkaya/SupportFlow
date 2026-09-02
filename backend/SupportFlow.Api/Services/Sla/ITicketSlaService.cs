using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;

namespace SupportFlow.Api.Services.Sla;

public interface ITicketSlaService
{
    bool IsTracked(Ticket ticket);

    void InitializeNewTicket(Ticket ticket);

    void RecordFirstSuccessfulStaffResponse(
        Ticket ticket,
        TicketMessage message,
        UserRole senderRole);

    void ApplyStatusTransition(
        Ticket ticket,
        TicketStatus oldStatus,
        TicketStatus newStatus,
        DateTime changedAt);

    void RecalculateUnfinishedDeadlines(Ticket ticket, TicketPriority newPriority);
}
