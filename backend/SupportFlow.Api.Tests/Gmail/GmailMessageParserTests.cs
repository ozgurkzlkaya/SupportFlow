using System.Text;
using Google.Apis.Gmail.v1.Data;
using SupportFlow.Api.Services.Gmail;
using SupportFlow.Api.Services.Gmail.Models;

namespace SupportFlow.Api.Tests.Gmail;

public sealed class GmailMessageParserTests
{
    [Fact]
    public void Parse_ReadsCorrelationHeadersCaseInsensitivelyAndNormalizesIds()
    {
        var message = new Message
        {
            Id = "gmail-message",
            ThreadId = "gmail-thread",
            InternalDate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Payload = new MessagePart
            {
                MimeType = "text/plain",
                Headers =
                [
                    new MessagePartHeader { Name = "fRoM", Value = "Customer <customer@example.test>" },
                    new MessagePartHeader { Name = "sUbJeCt", Value = "Reply" },
                    new MessagePartHeader { Name = "message-id", Value = "original@example.test" },
                    new MessagePartHeader { Name = "IN-REPLY-TO", Value = "<parent@example.test>" },
                    new MessagePartHeader
                    {
                        Name = "rEfErEnCeS",
                        Value = "<oldest@example.test> <parent@example.test>"
                    }
                ],
                Body = new MessagePartBody
                {
                    Data = EncodeBase64Url("Reply body")
                }
            }
        };

        var parsed = GmailMessageParser.Parse(message);

        Assert.Equal("<original@example.test>", parsed.InternetMessageId);
        Assert.Equal("<parent@example.test>", parsed.InReplyTo);
        Assert.Equal(
            ["<oldest@example.test>", "<parent@example.test>"],
            parsed.References);
        Assert.Empty(parsed.Attachments);
    }

    [Fact]
    public void ExtractAttachmentDescriptors_ReadsNestedMultipartAttachments()
    {
        var payload = new MessagePart
        {
            MimeType = "multipart/mixed",
            Parts =
            [
                new MessagePart
                {
                    MimeType = "multipart/alternative",
                    Parts =
                    [
                        new MessagePart
                        {
                            MimeType = "text/plain",
                            Body = new MessagePartBody { Data = EncodeBase64Url("Body") }
                        }
                    ]
                },
                new MessagePart
                {
                    MimeType = "image/png",
                    Filename = "vpn.png",
                    Body = new MessagePartBody { AttachmentId = "att-1" }
                },
                new MessagePart
                {
                    MimeType = "application/pdf",
                    Filename = "kanıt.pdf",
                    Body = new MessagePartBody { Data = EncodeBase64Url("pdf-binary") }
                }
            ]
        };

        var descriptors = GmailMessageParser.ExtractAttachmentDescriptors(payload);

        Assert.Equal(
            [
                new GmailAttachmentDescriptor("vpn.png", "image/png", "att-1", null),
                new GmailAttachmentDescriptor("kanıt.pdf", "application/pdf", null, EncodeBase64Url("pdf-binary"))
            ],
            descriptors);
    }

    [Theory]
    [InlineData("mailer-daemon@googlemail.com")]
    [InlineData("postmaster@example.test")]
    public void Parse_MarksRecognizedSystemSenderAsSystemGenerated(string sender)
    {
        var parsed = GmailMessageParser.Parse(CreateMessage(new MessagePart
        {
            MimeType = "text/plain",
            Headers =
            [
                new MessagePartHeader { Name = "From", Value = sender },
                new MessagePartHeader { Name = "Subject", Value = "Delivery Status Notification" }
            ],
            Body = new MessagePartBody { Data = EncodeBase64Url("Delivery report") }
        }));

        Assert.True(parsed.IsSystemGenerated);
    }

    [Fact]
    public void Parse_MarksAutoSubmittedMessageAsSystemGenerated()
    {
        var parsed = GmailMessageParser.Parse(CreateMessage(new MessagePart
        {
            MimeType = "text/plain",
            Headers =
            [
                new MessagePartHeader { Name = "From", Value = "automated@example.test" },
                new MessagePartHeader { Name = "Auto-Submitted", Value = "auto-replied" }
            ],
            Body = new MessagePartBody { Data = EncodeBase64Url("Automated reply") }
        }));

        Assert.True(parsed.IsSystemGenerated);
    }

    [Fact]
    public void Parse_MarksDeliveryStatusReportAsSystemGenerated()
    {
        var parsed = GmailMessageParser.Parse(CreateMessage(new MessagePart
        {
            MimeType = "multipart/report",
            Headers =
            [
                new MessagePartHeader { Name = "From", Value = "automated@example.test" },
                new MessagePartHeader
                {
                    Name = "Content-Type",
                    Value = "multipart/report; report-type=delivery-status"
                }
            ],
            Parts =
            [
                new MessagePart
                {
                    MimeType = "text/plain",
                    Body = new MessagePartBody { Data = EncodeBase64Url("Delivery report") }
                }
            ]
        }));

        Assert.True(parsed.IsSystemGenerated);
    }

    [Fact]
    public void Parse_MarksDeliveryStatusMimePartAsSystemGenerated()
    {
        var parsed = GmailMessageParser.Parse(CreateMessage(new MessagePart
        {
            MimeType = "multipart/mixed",
            Headers = [new MessagePartHeader { Name = "From", Value = "automated@example.test" }],
            Parts =
            [
                new MessagePart { MimeType = "message/delivery-status" },
                new MessagePart
                {
                    MimeType = "text/plain",
                    Body = new MessagePartBody { Data = EncodeBase64Url("Delivery report") }
                }
            ]
        }));

        Assert.True(parsed.IsSystemGenerated);
    }

    [Fact]
    public void Parse_DoesNotUseSubjectAloneToMarkSystemGenerated()
    {
        var parsed = GmailMessageParser.Parse(CreateMessage(new MessagePart
        {
            MimeType = "text/plain",
            Headers =
            [
                new MessagePartHeader { Name = "From", Value = "customer@example.test" },
                new MessagePartHeader { Name = "Subject", Value = "Delivery Status Notification" },
                new MessagePartHeader { Name = "Auto-Submitted", Value = "no" }
            ],
            Body = new MessagePartBody { Data = EncodeBase64Url("Please help with delivery status.") }
        }));

        Assert.False(parsed.IsSystemGenerated);
    }

    private static Message CreateMessage(MessagePart payload) => new()
    {
        Id = "gmail-message",
        ThreadId = "gmail-thread",
        InternalDate = new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(),
        Payload = payload
    };

    private static string EncodeBase64Url(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
