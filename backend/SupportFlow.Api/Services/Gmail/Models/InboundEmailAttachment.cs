namespace SupportFlow.Api.Services.Gmail.Models;

public sealed record InboundEmailAttachment(
    string FileName,
    string ContentType,
    string? GmailAttachmentId,
    byte[] ContentBytes,
    long FileSize);
