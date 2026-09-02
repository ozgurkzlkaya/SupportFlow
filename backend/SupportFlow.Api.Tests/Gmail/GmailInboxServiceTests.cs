using System.Text;
using SupportFlow.Api.Services.Gmail;
using SupportFlow.Api.Services.Gmail.Models;

namespace SupportFlow.Api.Tests.Gmail;

public sealed class GmailInboxServiceTests
{
    [Fact]
    public void CreateDiagnostic_UsesRawFromParsedSenderInternalDateAndAttachmentMetadata()
    {
        var internalDate = new DateTimeOffset(2026, 8, 21, 8, 51, 0, TimeSpan.Zero)
            .ToUnixTimeMilliseconds();
        var message = new Google.Apis.Gmail.v1.Data.Message
        {
            Id = "gmail-message-1",
            ThreadId = "gmail-thread-1",
            InternalDate = internalDate,
            Payload = new Google.Apis.Gmail.v1.Data.MessagePart
            {
                Headers =
                [
                    new Google.Apis.Gmail.v1.Data.MessagePartHeader
                    {
                        Name = "From",
                        Value = "Takashi Hyugo <takashihyugo@gmail.com>"
                    },
                    new Google.Apis.Gmail.v1.Data.MessagePartHeader
                    {
                        Name = "Subject",
                        Value = "VPN bağlantısı"
                    }
                ],
                Parts =
                [
                    new Google.Apis.Gmail.v1.Data.MessagePart
                    {
                        Filename = "vpn.png",
                        MimeType = "image/png",
                        Body = new Google.Apis.Gmail.v1.Data.MessagePartBody
                        {
                            AttachmentId = "attachment-1"
                        }
                    }
                ]
            }
        };

        var diagnostic = GmailInboxService.CreateDiagnostic(message);

        Assert.Equal("Takashi Hyugo <takashihyugo@gmail.com>", diagnostic.RawFrom);
        Assert.Equal("takashihyugo@gmail.com", diagnostic.ParsedSenderEmail);
        Assert.Equal(new DateTime(2026, 8, 21, 8, 51, 0, DateTimeKind.Utc), diagnostic.InternalDateUtc);
        Assert.Equal("2026-08-21 11:51:00", diagnostic.InternalDateEuropeIstanbul);
        Assert.True(diagnostic.HasAttachments);
        Assert.Equal(1, diagnostic.AttachmentCount);
        Assert.Equal(["vpn.png"], diagnostic.AttachmentFileNames);
    }

    [Fact]
    public void BuildMailboxDiscoveryQuery_UsesBoundedRecentInboxWindowAndMailboxExclusion()
    {
        var query = GmailInboxService.BuildMailboxDiscoveryQuery("support11flow@gmail.com");

        Assert.Equal("in:inbox newer_than:7d -from:support11flow@gmail.com", query);
    }

    [Fact]
    public void BuildMailboxDiscoveryQuery_WithoutMailboxAddress_StillUsesBoundedRecentInboxWindow()
    {
        var query = GmailInboxService.BuildMailboxDiscoveryQuery("  ");

        Assert.Equal("in:inbox newer_than:7d", query);
    }

    [Fact]
    public void BuildMailboxDiscoveryQuery_DoesNotDependOnUnreadState()
    {
        var query = GmailInboxService.BuildMailboxDiscoveryQuery("support11flow@gmail.com");

        Assert.DoesNotContain("is:unread", query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildMailboxDiscoveryQuery_UsesRecentWindowToExcludeOldMessages()
    {
        var query = GmailInboxService.BuildMailboxDiscoveryQuery("support11flow@gmail.com");

        Assert.Contains($"newer_than:{GmailInboxService.RecentInboxWindowDays}d", query);
    }

    [Fact]
    public void BuildBackgroundDiscoveryQuery_OnRestartUsesBoundedOneDayCatchUp()
    {
        var query = GmailInboxService.BuildBackgroundDiscoveryQuery(
            "support11flow@gmail.com",
            null);

        Assert.Equal("in:inbox newer_than:1d -from:support11flow@gmail.com", query.Query);
        Assert.True(query.IsRestartCatchUp);
        Assert.DoesNotContain("is:unread", query.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildBackgroundDiscoveryQuery_AfterSuccessUsesOneDayOverlapIncrementalDate()
    {
        var query = GmailInboxService.BuildBackgroundDiscoveryQuery(
            "support11flow@gmail.com",
            new DateTime(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc));

        Assert.Equal("in:inbox after:2026/08/22 -from:support11flow@gmail.com", query.Query);
        Assert.False(query.IsRestartCatchUp);
    }

    [Fact]
    public async Task ResolveAttachmentsAsync_UsesAttachmentIdResolverAndInlineData()
    {
        var descriptors = new[]
        {
            new GmailAttachmentDescriptor("vpn.png", "image/png", "att-1", null),
            new GmailAttachmentDescriptor(
                "kanıt.pdf",
                "application/pdf",
                null,
                EncodeBase64Url("pdf-bytes"))
        };

        var attachments = await GmailInboxService.ResolveAttachmentsAsync(
            "gmail-message-1",
            descriptors,
            (messageId, attachmentId, _) =>
            {
                Assert.Equal("gmail-message-1", messageId);
                Assert.Equal("att-1", attachmentId);
                return Task.FromResult<string?>(EncodeBase64Url("png-bytes"));
            },
            null,
            CancellationToken.None);

        Assert.Collection(
            attachments,
            attachment =>
            {
                Assert.Equal("vpn.png", attachment.FileName);
                Assert.Equal("image/png", attachment.ContentType);
                Assert.Equal("att-1", attachment.GmailAttachmentId);
                Assert.Equal(Encoding.UTF8.GetBytes("png-bytes"), attachment.ContentBytes);
            },
            attachment =>
            {
                Assert.Equal("kanıt.pdf", attachment.FileName);
                Assert.Equal("application/pdf", attachment.ContentType);
                Assert.Null(attachment.GmailAttachmentId);
                Assert.Equal(Encoding.UTF8.GetBytes("pdf-bytes"), attachment.ContentBytes);
            });
    }

    private static string EncodeBase64Url(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
