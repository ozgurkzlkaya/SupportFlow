using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google;
using System.Globalization;
using Microsoft.Extensions.Options;
using SupportFlow.Api.Services.Gmail.Models;
using SupportFlow.Api.Settings;

namespace SupportFlow.Api.Services.Gmail;

public class GmailInboxService : IGmailInboxService
{
    internal const int RecentInboxWindowDays = 7;
    internal const int RestartCatchUpWindowDays = 1;

    private readonly IGmailAuthorizationService _gmailAuthorizationService;
    private readonly string _mailboxAddress;
    private readonly string _applicationName;
    private readonly ILogger<GmailInboxService> _logger;
    private readonly IGmailApiRateLimitState _rateLimitState;

    public GmailInboxService(
        IGmailAuthorizationService gmailAuthorizationService,
        IOptions<GmailSettings> settings,
        ILogger<GmailInboxService> logger,
        IGmailApiRateLimitState rateLimitState)
    {
        _gmailAuthorizationService = gmailAuthorizationService;
        _mailboxAddress = settings.Value.MailboxAddress;
        _applicationName = settings.Value.ApplicationName;
        _logger = logger;
        _rateLimitState = rateLimitState;
    }

    public async Task<IReadOnlyList<string>> ListRecentInboxMessageIdsAsync(
        int maximumResults = 25,
        CancellationToken cancellationToken = default,
        GmailInboxDiscoveryQuery? discoveryQuery = null)
    {
        if (maximumResults is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumResults),
                "Maximum results must be between 1 and 100.");
        }

        ThrowIfRateLimited();
        using var service = await CreateServiceAsync(cancellationToken);
        var listRequest = service.Users.Messages.List("me");
        listRequest.Q = discoveryQuery?.Query ?? BuildMailboxDiscoveryQuery(_mailboxAddress);
        listRequest.MaxResults = maximumResults;
        listRequest.IncludeSpamTrash = false;

        var response = await ExecuteReadAsync(
            () => listRequest.ExecuteAsync(cancellationToken));
        return response.Messages?
            .Where(message => !string.IsNullOrWhiteSpace(message.Id))
            .Select(message => message.Id)
            .ToList()
            ?? [];
    }

    public async Task<IReadOnlyList<InboundEmailMessage>> GetRecentInboxMessagesAsync(
        int maximumResults = 25,
        CancellationToken cancellationToken = default)
    {
        var messageIds = await ListRecentInboxMessageIdsAsync(
            maximumResults,
            cancellationToken);
        if (messageIds.Count == 0)
        {
            return [];
        }

        var messages = new List<InboundEmailMessage>(messageIds.Count);
        foreach (var messageId in messageIds)
        {
            var message = await GetMessageAsync(messageId, cancellationToken);
            if (message is not null)
            {
                messages.Add(message);
            }
        }

        _logger.LogInformation(
            "Read {MessageCount} recent inbox Gmail messages for mailbox {MailboxAddress}.",
            messages.Count,
            _mailboxAddress);

        return messages;
    }

    public async Task<IReadOnlyList<GmailMessageDiagnostic>> GetRecentMessageDiagnosticsAsync(
        int maximumResults = 25,
        CancellationToken cancellationToken = default)
    {
        if (maximumResults is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumResults),
                "Maximum results must be between 1 and 100.");
        }

        ThrowIfRateLimited();
        using var service = await CreateServiceAsync(cancellationToken);
        var listRequest = service.Users.Messages.List("me");
        listRequest.Q = BuildMailboxDiscoveryQuery(_mailboxAddress);
        listRequest.MaxResults = maximumResults;
        listRequest.IncludeSpamTrash = false;

        var response = await ExecuteReadAsync(
            () => listRequest.ExecuteAsync(cancellationToken));
        var messageIds = response.Messages?
            .Where(message => !string.IsNullOrWhiteSpace(message.Id))
            .Select(message => message.Id!)
            .ToList()
            ?? [];
        var diagnostics = new List<GmailMessageDiagnostic>(messageIds.Count);
        foreach (var messageId in messageIds)
        {
            var request = service.Users.Messages.Get("me", messageId);
            request.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Full;
            var message = await ExecuteReadAsync(
                () => request.ExecuteAsync(cancellationToken));
            diagnostics.Add(CreateDiagnostic(message));
        }

        return diagnostics;
    }

    internal static string BuildMailboxDiscoveryQuery(string? mailboxAddress)
    {
        var baseQuery = $"in:inbox newer_than:{RecentInboxWindowDays}d";
        return string.IsNullOrWhiteSpace(mailboxAddress)
            ? baseQuery
            : $"{baseQuery} -from:{mailboxAddress.Trim()}";
    }

    internal static GmailInboxDiscoveryQuery BuildBackgroundDiscoveryQuery(
        string? mailboxAddress,
        DateTime? lastSuccessfulCycleStartedAtUtc)
    {
        var mailboxExclusion = string.IsNullOrWhiteSpace(mailboxAddress)
            ? string.Empty
            : $" -from:{mailboxAddress.Trim()}";
        if (lastSuccessfulCycleStartedAtUtc is null)
        {
            return new GmailInboxDiscoveryQuery(
                $"in:inbox newer_than:{RestartCatchUpWindowDays}d{mailboxExclusion}",
                IsRestartCatchUp: true);
        }

        // Gmail's date search is day-granular. Keep a one-day overlap and rely on
        // the existing GmailMessageId deduplication to avoid missing boundary mail.
        var startDate = lastSuccessfulCycleStartedAtUtc.Value
            .ToUniversalTime()
            .Date
            .AddDays(-1);
        return new GmailInboxDiscoveryQuery(
            $"in:inbox after:{startDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture)}{mailboxExclusion}",
            IsRestartCatchUp: false);
    }

    public async Task<InboundEmailMessage?> GetMessageAsync(
        string gmailMessageId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gmailMessageId))
        {
            throw new ArgumentException("Gmail message ID is required.", nameof(gmailMessageId));
        }

        ThrowIfRateLimited();
        using var service = await CreateServiceAsync(cancellationToken);
        return await GetMessageAsync(service, gmailMessageId, cancellationToken);
    }

    public async Task<IReadOnlyList<InboundEmailAttachment>> ResolveMessageAttachmentsAsync(
        InboundEmailMessage message,
        CancellationToken cancellationToken = default)
    {
        var descriptors = message.AttachmentDescriptors ?? [];
        if (descriptors.Count == 0)
        {
            return [];
        }

        ThrowIfRateLimited();
        using var service = await CreateServiceAsync(cancellationToken);
        return await ResolveAttachmentsAsync(
            message.GmailMessageId,
            descriptors,
            (messageId, attachmentId, token) => GetAttachmentDataAsync(
                service,
                messageId,
                attachmentId,
                token),
            (descriptor, exception) =>
            {
                _logger.LogWarning(
                    exception,
                    "Skipped Gmail attachment {FileName} for message {GmailMessageId}.",
                    descriptor.FileName,
                    message.GmailMessageId);
            },
            cancellationToken);
    }

    internal static async Task<IReadOnlyList<InboundEmailAttachment>> ResolveAttachmentsAsync(
        string gmailMessageId,
        IReadOnlyCollection<GmailAttachmentDescriptor> descriptors,
        Func<string, string, CancellationToken, Task<string?>> attachmentDataResolver,
        Action<GmailAttachmentDescriptor, Exception>? onResolveFailure,
        CancellationToken cancellationToken)
    {
        if (descriptors.Count == 0)
        {
            return [];
        }

        var attachments = new List<InboundEmailAttachment>(descriptors.Count);
        foreach (var descriptor in descriptors)
        {
            try
            {
                var encodedData = descriptor.InlineData;
                if (string.IsNullOrWhiteSpace(encodedData)
                    && !string.IsNullOrWhiteSpace(descriptor.GmailAttachmentId))
                {
                    encodedData = await attachmentDataResolver(
                        gmailMessageId,
                        descriptor.GmailAttachmentId,
                        cancellationToken);
                }

                if (string.IsNullOrWhiteSpace(encodedData))
                {
                    continue;
                }

                var bytes = GmailMessageParser.DecodeBase64UrlBytes(encodedData);
                attachments.Add(new InboundEmailAttachment(
                    descriptor.FileName,
                    string.IsNullOrWhiteSpace(descriptor.ContentType)
                        ? "application/octet-stream"
                        : descriptor.ContentType,
                    descriptor.GmailAttachmentId,
                    bytes,
                    bytes.LongLength));
            }
            catch (GmailRateLimitedException)
            {
                throw;
            }
            catch (Exception exception)
            {
                onResolveFailure?.Invoke(descriptor, exception);
            }
        }

        return attachments;
    }

    internal static GmailMessageDiagnostic CreateDiagnostic(Message message)
    {
        var payload = message.Payload
            ?? throw new InvalidOperationException("Gmail message payload is missing.");
        var rawFrom = GmailMessageParser.GetHeaderValue(payload, "From");
        var (senderEmail, _) = GmailMessageParser.ParseSenderHeader(rawFrom);
        var subject = GmailMessageParser.GetHeaderValue(payload, "Subject");
        var internalDateUtc = message.InternalDate.HasValue
            ? DateTimeOffset.FromUnixTimeMilliseconds(message.InternalDate.Value).UtcDateTime
            : DateTime.UtcNow;
        var attachmentDescriptors = GmailMessageParser.ExtractAttachmentDescriptors(payload);
        var istanbulTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

        return new GmailMessageDiagnostic(
            message.Id ?? string.Empty,
            message.ThreadId ?? string.Empty,
            string.IsNullOrWhiteSpace(subject) ? "(Konu yok)" : subject.Trim(),
            rawFrom,
            senderEmail,
            internalDateUtc,
            TimeZoneInfo.ConvertTimeFromUtc(internalDateUtc, istanbulTimeZone)
                .ToString("yyyy-MM-dd HH:mm:ss"),
            attachmentDescriptors.Count > 0,
            attachmentDescriptors.Count,
            attachmentDescriptors.Select(descriptor => descriptor.FileName).ToArray());
    }

    private async Task<InboundEmailMessage> GetMessageAsync(
        GmailService service,
        string gmailMessageId,
        CancellationToken cancellationToken)
    {
        var request = service.Users.Messages.Get("me", gmailMessageId);
        request.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Full;
        var message = await ExecuteReadAsync(
            () => request.ExecuteAsync(cancellationToken));
        var parsed = GmailMessageParser.Parse(message);
        return parsed with
        {
            AttachmentDescriptors = GmailMessageParser.ExtractAttachmentDescriptors(message.Payload)
        };
    }

    private async Task<string?> GetAttachmentDataAsync(
        GmailService service,
        string gmailMessageId,
        string attachmentId,
        CancellationToken cancellationToken)
    {
        var request = service.Users.Messages.Attachments.Get("me", gmailMessageId, attachmentId);
        var response = await ExecuteReadAsync(
            () => request.ExecuteAsync(cancellationToken));
        return response.Data;
    }

    private async Task<T> ExecuteReadAsync<T>(Func<Task<T>> operation)
    {
        ThrowIfRateLimited();

        try
        {
            var result = await operation();
            _rateLimitState.MarkSuccessfulCall();
            return result;
        }
        catch (GoogleApiException exception)
            when (GmailApiRateLimitDetector.TryHandle(
                exception,
                _rateLimitState,
                out var rateLimitedException))
        {
            _logger.LogWarning(
                "Gmail API rate limit reached. Requests are paused until {RetryAtUtc}.",
                rateLimitedException.RetryAtUtc);
            throw rateLimitedException;
        }
    }

    private void ThrowIfRateLimited()
    {
        if (_rateLimitState.TryGetRateLimitedUntil(out var retryAt))
        {
            throw new GmailRateLimitedException(retryAt);
        }
    }

    private async Task<GmailService> CreateServiceAsync(
        CancellationToken cancellationToken)
    {
        var credential = await _gmailAuthorizationService.GetAuthorizedCredentialAsync(
            cancellationToken);

        return new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = _applicationName
        });
    }
}
