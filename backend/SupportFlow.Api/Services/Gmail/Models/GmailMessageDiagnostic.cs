using SupportFlow.Api.Enums;

namespace SupportFlow.Api.Services.Gmail.Models;

public sealed record GmailMessageDiagnostic(
    string GmailMessageId,
    string GmailThreadId,
    string Subject,
    string RawFrom,
    string ParsedSenderEmail,
    DateTime InternalDateUtc,
    string InternalDateEuropeIstanbul,
    bool HasAttachments,
    int AttachmentCount,
    IReadOnlyList<string> AttachmentFileNames);

public sealed record GmailMessageDiagnosticResponse(
    string GmailMessageId,
    string GmailThreadId,
    string Subject,
    string RawFrom,
    string ParsedSenderEmail,
    DateTime InternalDateUtc,
    string InternalDateEuropeIstanbul,
    bool HasAttachments,
    int AttachmentCount,
    IReadOnlyList<string> AttachmentFileNames,
    bool IsAlreadyProcessed,
    bool UserExists,
    int? MatchingSupportFlowUserId,
    UserRole? MatchingSupportFlowUserRole,
    bool? MatchingSupportFlowUserIsActive,
    bool RegisteredUserMatch,
    bool ExternalRequesterMatch,
    int? ExternalRequesterId,
    string RequesterType,
    bool IsCached,
    DateTime? CachedAt);
