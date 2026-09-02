using Google.Apis.Gmail.v1.Data;

namespace SupportFlow.Api.Services.Gmail;

internal static class GmailSystemMessageDetector
{
    public static bool IsSystemMessage(MessagePart payload, string senderEmail)
    {
        return IsMailSystemSender(senderEmail)
            || HasAutomatedSubmissionHeader(payload)
            || HasDeliveryStatusPart(payload);
    }

    private static bool IsMailSystemSender(string senderEmail)
    {
        var normalized = senderEmail.Trim().ToLowerInvariant();
        return normalized.StartsWith("mailer-daemon@", StringComparison.Ordinal)
            || normalized.StartsWith("postmaster@", StringComparison.Ordinal);
    }

    private static bool HasAutomatedSubmissionHeader(MessagePart payload)
    {
        var autoSubmitted = GmailMessageParser.GetHeaderValue(payload, "Auto-Submitted");
        return !string.IsNullOrWhiteSpace(autoSubmitted)
            && !string.Equals(autoSubmitted.Trim(), "no", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasDeliveryStatusPart(MessagePart part)
    {
        if (string.Equals(part.MimeType, "message/delivery-status", StringComparison.OrdinalIgnoreCase)
            || IsDeliveryStatusReport(GmailMessageParser.GetHeaderValue(part, "Content-Type")))
        {
            return true;
        }

        return part.Parts?.Any(HasDeliveryStatusPart) is true;
    }

    private static bool IsDeliveryStatusReport(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var normalized = contentType
            .Replace("\"", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        return normalized.Contains("multipart/report", StringComparison.Ordinal)
            && normalized.Contains("report-type=delivery-status", StringComparison.Ordinal);
    }
}
