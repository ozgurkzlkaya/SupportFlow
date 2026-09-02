namespace SupportFlow.Api.Services.Gmail.Models;

public sealed record InboundEmailMessage(
    string GmailMessageId,
    string GmailThreadId,
    string? InternetMessageId,
    string? InReplyTo,
    IReadOnlyCollection<string> References,
    string FromEmail,
    string? FromName,
    string Subject,
    string Body,
    DateTime ReceivedAt,
    IReadOnlyCollection<InboundEmailAttachment> Attachments,
    IReadOnlyCollection<GmailAttachmentDescriptor>? AttachmentDescriptors = null,
    bool IsSystemGenerated = false);
