using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using Google.Apis.Gmail.v1.Data;
using SupportFlow.Api.Services.Gmail.Models;

namespace SupportFlow.Api.Services.Gmail;

internal static partial class GmailMessageParser
{
    public static InboundEmailMessage Parse(Message message)
    {
        var payload = message.Payload
            ?? throw new InvalidOperationException("Gmail message payload is missing.");
        var fromHeader = GetHeaderValue(payload, "From");
        var (fromEmail, fromName) = ParseSenderHeader(fromHeader);
        var subject = GetHeaderValue(payload, "Subject");
        var internetMessageId = NormalizeMessageId(GetHeaderValue(payload, "Message-ID"));
        var inReplyTo = ParseMessageIds(GetHeaderValue(payload, "In-Reply-To"))
            .FirstOrDefault();
        var references = ParseMessageIds(GetHeaderValue(payload, "References"));
        var body = GetPreferredBody(payload);
        var isSystemGenerated = GmailSystemMessageDetector.IsSystemMessage(payload, fromEmail);
        var receivedAt = message.InternalDate.HasValue
            ? DateTimeOffset.FromUnixTimeMilliseconds(message.InternalDate.Value).UtcDateTime
            : DateTime.UtcNow;

        return new InboundEmailMessage(
            message.Id ?? string.Empty,
            message.ThreadId ?? string.Empty,
            internetMessageId,
            inReplyTo,
            references,
            fromEmail,
            fromName,
            string.IsNullOrWhiteSpace(subject) ? "(Konu yok)" : subject.Trim(),
            CleanBody(body),
            receivedAt,
            [],
            IsSystemGenerated: isSystemGenerated);
    }

    internal static IReadOnlyCollection<GmailAttachmentDescriptor> ExtractAttachmentDescriptors(
        MessagePart? payload)
    {
        if (payload is null)
        {
            return [];
        }

        var descriptors = new List<GmailAttachmentDescriptor>();
        CollectAttachmentDescriptors(payload, descriptors);
        return descriptors;
    }

    internal static string? NormalizeMessageId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = AngleBracketMessageIdRegex().Match(value);
        var identifier = match.Success
            ? match.Value[1..^1].Trim()
            : value.Trim().Trim('<', '>').Trim();

        if (identifier.Length == 0
            || identifier.Length > 996
            || identifier.IndexOfAny(['\r', '\n']) >= 0)
        {
            return null;
        }

        return $"<{identifier}>";
    }

    internal static IReadOnlyCollection<string> ParseMessageIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var bracketedIds = AngleBracketMessageIdRegex()
            .Matches(value)
            .Select(match => NormalizeMessageId(match.Value))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (bracketedIds.Length > 0)
        {
            return bracketedIds;
        }

        return value
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeMessageId)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    internal static string GetHeaderValue(MessagePart payload, string name) =>
        payload.Headers?
            .FirstOrDefault(header =>
                string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase))
            ?.Value
        ?? string.Empty;

    internal static (string Email, string? Name) ParseSenderHeader(string value)
    {
        try
        {
            var address = new MailAddress(value);
            return (
                address.Address.Trim().ToLowerInvariant(),
                string.IsNullOrWhiteSpace(address.DisplayName)
                    ? null
                    : address.DisplayName.Trim());
        }
        catch (FormatException)
        {
            var match = EmailAddressRegex().Match(value);
            return match.Success
                ? (match.Value.Trim().ToLowerInvariant(), null)
                : (value.Trim().ToLowerInvariant(), null);
        }
    }

    private static string GetPreferredBody(MessagePart payload)
    {
        var plainText = FindBody(payload, "text/plain");
        if (!string.IsNullOrWhiteSpace(plainText))
        {
            return plainText;
        }

        var html = FindBody(payload, "text/html");
        return string.IsNullOrWhiteSpace(html) ? string.Empty : ConvertHtmlToText(html);
    }

    private static string? FindBody(MessagePart part, string mimeType)
    {
        if (string.Equals(part.MimeType, mimeType, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(part.Filename)
            && !string.IsNullOrWhiteSpace(part.Body?.Data))
        {
            return DecodeBase64Url(part.Body.Data);
        }

        if (part.Parts is null)
        {
            return null;
        }

        foreach (var child in part.Parts)
        {
            var body = FindBody(child, mimeType);
            if (!string.IsNullOrWhiteSpace(body))
            {
                return body;
            }
        }

        return null;
    }

    internal static byte[] DecodeBase64UrlBytes(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        return Convert.FromBase64String(base64);
    }

    internal static string DecodeBase64Url(string value)
    {
        return Encoding.UTF8.GetString(DecodeBase64UrlBytes(value));
    }

    private static string ConvertHtmlToText(string html)
    {
        var withLineBreaks = HtmlLineBreakRegex().Replace(html, "\n");
        var withoutTags = HtmlTagRegex().Replace(withLineBreaks, string.Empty);
        return WebUtility.HtmlDecode(withoutTags);
    }

    private static string CleanBody(string body)
    {
        var normalized = body.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var keptLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed == "--"
                || trimmed.Equals("-----Original Message-----", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("On ", StringComparison.OrdinalIgnoreCase)
                    && trimmed.EndsWith(" wrote:", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            keptLines.Add(line.TrimEnd());
        }

        return ConsecutiveBlankLinesRegex()
            .Replace(string.Join('\n', keptLines).Trim(), "\n\n");
    }

    private static void CollectAttachmentDescriptors(
        MessagePart part,
        ICollection<GmailAttachmentDescriptor> descriptors)
    {
        if (!string.IsNullOrWhiteSpace(part.Filename)
            && (!string.IsNullOrWhiteSpace(part.Body?.AttachmentId)
                || !string.IsNullOrWhiteSpace(part.Body?.Data)))
        {
            descriptors.Add(new GmailAttachmentDescriptor(
                part.Filename,
                part.MimeType ?? "application/octet-stream",
                part.Body?.AttachmentId,
                part.Body?.Data));
        }

        if (part.Parts is null)
        {
            return;
        }

        foreach (var child in part.Parts)
        {
            CollectAttachmentDescriptors(child, descriptors);
        }
    }

    [GeneratedRegex(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase)]
    private static partial Regex EmailAddressRegex();

    [GeneratedRegex(@"<[^<>\r\n]+>")]
    private static partial Regex AngleBracketMessageIdRegex();

    [GeneratedRegex(@"<(br\s*/?|/p|/div)>", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlLineBreakRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ConsecutiveBlankLinesRegex();
}
