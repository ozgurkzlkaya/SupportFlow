namespace SupportFlow.Api.Services.Gmail.Models;

public enum InboundEmailProcessingStatus
{
    NewTicketCreated,
    NewExternalTicketCreated,
    MessageAdded,
    AlreadyProcessed,
    UnknownCustomer,
    SenderMismatch,
    SelfMessageIgnored,
    SystemMessageSkipped,
    Failed
}

public sealed record InboundEmailProcessingResult(
    InboundEmailProcessingStatus Status,
    int? CustomerId = null,
    int? ExternalRequesterId = null,
    bool? ExternalRequesterWasCreated = null,
    int? TicketId = null,
    string? TicketNumber = null,
    int? TicketMessageId = null,
    string? Reason = null,
    int AttachmentsFetched = 0)
{
    public static InboundEmailProcessingResult NewTicket(
        int customerId,
        int ticketId,
        string ticketNumber) =>
        new(
            InboundEmailProcessingStatus.NewTicketCreated,
            CustomerId: customerId,
            TicketId: ticketId,
            TicketNumber: ticketNumber);

    public static InboundEmailProcessingResult NewExternalTicket(
        int externalRequesterId,
        int ticketId,
        string ticketNumber,
        bool externalRequesterWasCreated) =>
        new(
            InboundEmailProcessingStatus.NewExternalTicketCreated,
            ExternalRequesterId: externalRequesterId,
            ExternalRequesterWasCreated: externalRequesterWasCreated,
            TicketId: ticketId,
            TicketNumber: ticketNumber);

    public static InboundEmailProcessingResult MessageAdded(
        int customerId,
        int ticketId,
        string ticketNumber,
        int ticketMessageId) =>
        new(
            InboundEmailProcessingStatus.MessageAdded,
            CustomerId: customerId,
            TicketId: ticketId,
            TicketNumber: ticketNumber,
            TicketMessageId: ticketMessageId);

    public static InboundEmailProcessingResult ExternalRequesterMessageAdded(
        int externalRequesterId,
        int ticketId,
        string ticketNumber,
        int ticketMessageId) =>
        new(
            InboundEmailProcessingStatus.MessageAdded,
            ExternalRequesterId: externalRequesterId,
            TicketId: ticketId,
            TicketNumber: ticketNumber,
            TicketMessageId: ticketMessageId);

    public static InboundEmailProcessingResult AlreadyProcessed(
        int ticketId,
        string ticketNumber) =>
        new(
            InboundEmailProcessingStatus.AlreadyProcessed,
            TicketId: ticketId,
            TicketNumber: ticketNumber,
            Reason: "The Gmail message was already processed.");

    public static InboundEmailProcessingResult UnknownCustomer(string reason) =>
        new(
            InboundEmailProcessingStatus.UnknownCustomer,
            Reason: reason);

    public static InboundEmailProcessingResult SenderMismatch(
        int ticketId,
        string ticketNumber) =>
        new(
            InboundEmailProcessingStatus.SenderMismatch,
            TicketId: ticketId,
            TicketNumber: ticketNumber,
            Reason: "The sender does not own the correlated ticket.");

    public static InboundEmailProcessingResult SelfMessageIgnored() =>
        new(
            InboundEmailProcessingStatus.SelfMessageIgnored,
            Reason: "Messages sent from the SupportFlow mailbox are ignored.");

    public static InboundEmailProcessingResult SystemMessageSkipped() =>
        new(
            InboundEmailProcessingStatus.SystemMessageSkipped,
            Reason: "Sistem tarafından oluşturulan e-posta iletisi atlandı.");

    public static InboundEmailProcessingResult Failed(string reason) =>
        new(InboundEmailProcessingStatus.Failed, Reason: reason);
}
