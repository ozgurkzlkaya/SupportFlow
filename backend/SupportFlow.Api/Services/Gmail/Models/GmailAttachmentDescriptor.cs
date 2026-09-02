namespace SupportFlow.Api.Services.Gmail.Models;

public sealed record GmailAttachmentDescriptor(
    string FileName,
    string ContentType,
    string? GmailAttachmentId,
    string? InlineData);
