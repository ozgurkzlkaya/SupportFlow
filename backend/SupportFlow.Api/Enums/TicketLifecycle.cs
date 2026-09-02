namespace SupportFlow.Api.Enums;

public static class TicketLifecycle
{
    public static readonly TicketStatus[] ActiveStatuses =
    [
        TicketStatus.New,
        TicketStatus.Assigned,
        TicketStatus.InProgress,
        TicketStatus.WaitingForCustomer,
        TicketStatus.Reopened
    ];
}
