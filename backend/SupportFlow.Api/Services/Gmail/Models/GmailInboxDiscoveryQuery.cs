namespace SupportFlow.Api.Services.Gmail.Models;

public sealed record GmailInboxDiscoveryQuery(
    string Query,
    bool IsRestartCatchUp);
