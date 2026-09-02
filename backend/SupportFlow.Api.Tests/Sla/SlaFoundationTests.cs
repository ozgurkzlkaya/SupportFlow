using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Options;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Migrations;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services.Sla;
using SupportFlow.Api.Settings;

namespace SupportFlow.Api.Tests.Sla;

public sealed class SlaFoundationTests
{
    [Theory]
    [InlineData(TicketPriority.Low, 1440, 7200)]
    [InlineData(TicketPriority.Normal, 480, 2880)]
    [InlineData(TicketPriority.High, 120, 960)]
    [InlineData(TicketPriority.Critical, 30, 240)]
    public void Resolver_ReturnsApprovedTargets(
        TicketPriority priority,
        int firstResponseMinutes,
        int resolutionMinutes)
    {
        var targets = CreateResolver().GetTargets(priority);

        Assert.Equal(TimeSpan.FromMinutes(firstResponseMinutes), targets.FirstResponse);
        Assert.Equal(TimeSpan.FromMinutes(resolutionMinutes), targets.Resolution);
    }

    [Fact]
    public void Resolver_RejectsInvalidPriority()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateResolver().GetTargets((TicketPriority)999));
    }

    [Fact]
    public void Validator_RejectsNonPositiveFirstResponseTarget()
    {
        var settings = new SlaSettings { Low = new SlaPriorityTargets(0, 7200) };

        var result = new SlaSettingsValidator().Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("Low:FirstResponseMinutes"));
    }

    [Fact]
    public void Validator_RejectsNonPositiveResolutionTarget()
    {
        var settings = new SlaSettings { Normal = new SlaPriorityTargets(480, 0) };

        var result = new SlaSettingsValidator().Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("Normal:ResolutionMinutes"));
    }

    [Fact]
    public void Validator_RejectsResolutionShorterThanFirstResponse()
    {
        var settings = new SlaSettings { High = new SlaPriorityTargets(120, 119) };

        var result = new SlaSettingsValidator().Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("greater than or equal"));
    }

    [Fact]
    public void Migration_AddsOnlyNullableSlaColumns()
    {
        var operations = GetUpOperations();
        var columns = operations.OfType<AddColumnOperation>().ToArray();

        Assert.Equal(
        [
            "FirstRespondedAt",
            "FirstResponseDueAt",
            "ResolutionCycleStartedAt",
            "ResolutionDueAt",
            "ResolvedAt"
        ], columns.Select(column => column.Name).OrderBy(name => name));
        Assert.All(columns, column =>
        {
            Assert.Equal("Tickets", column.Table);
            Assert.Equal("datetime2", column.ColumnType);
            Assert.True(column.IsNullable);
        });
    }

    [Fact]
    public void Migration_AddsSlaDeadlineIndexes()
    {
        var indexes = GetUpOperations().OfType<CreateIndexOperation>().ToArray();

        Assert.Equal(
        ["IX_Tickets_FirstResponseDueAt", "IX_Tickets_ResolutionDueAt"],
        indexes.Select(index => index.Name).OrderBy(name => name));
    }

    [Fact]
    public void Migration_DoesNotBackfillOrMutateExistingTickets()
    {
        var operations = GetUpOperations();

        Assert.DoesNotContain(operations, operation => operation is SqlOperation
            or UpdateDataOperation
            or InsertDataOperation
            or DeleteDataOperation
            or AlterColumnOperation);
    }

    [Fact]
    public void LegacyTicket_ModelDefaultsRemainSlaUntracked()
    {
        var ticket = new Ticket();

        Assert.Null(ticket.FirstResponseDueAt);
        Assert.Null(ticket.FirstRespondedAt);
        Assert.Null(ticket.ResolutionCycleStartedAt);
        Assert.Null(ticket.ResolutionDueAt);
        Assert.Null(ticket.ResolvedAt);
    }

    private static SlaTargetResolver CreateResolver() =>
        new(Options.Create(new SlaSettings()));

    private static IReadOnlyList<MigrationOperation> GetUpOperations()
    {
        var migration = new AddTicketSlaFoundation();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var method = typeof(AddTicketSlaFoundation).GetMethod(
            "Up",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(migration, [builder]);
        return builder.Operations;
    }
}
