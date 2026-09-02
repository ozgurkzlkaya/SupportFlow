using Microsoft.Extensions.Options;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services.Email;
using SupportFlow.Api.Settings;

namespace SupportFlow.Api.Tests.Email;

public sealed class TicketEmailComposerTests
{
    [Fact]
    public void CustomerFacingSubjects_UseStableBracketedTicketNumberAndTitle()
    {
        var composer = new TicketEmailComposer(
            Options.Create(new ApplicationSettings()));
        var ticket = new Ticket
        {
            TicketNumber = "SUP-2026-001005",
            Title = "Yazıcı bağlantı sorunu"
        };
        var sender = new User { FirstName = "Tech", LastName = "User" };

        var reply = composer.ComposeTechnicalReply(ticket, sender, "Reply");
        var status = composer.ComposeStatusChange(
            ticket,
            TicketStatus.New,
            TicketStatus.InProgress,
            null);

        Assert.Equal("[SUP-2026-001005] Yazıcı bağlantı sorunu", reply.Subject);
        Assert.Equal(reply.Subject, status.Subject);
    }
}
