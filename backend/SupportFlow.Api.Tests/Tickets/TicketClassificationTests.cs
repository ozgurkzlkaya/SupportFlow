using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SupportFlow.Api.Controllers;
using SupportFlow.Api.DTOs.Tickets;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services;

namespace SupportFlow.Api.Tests.Tickets;

public sealed class TicketClassificationTests
{
    private static readonly DateTime Now = new(2026, 8, 25, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ClassificationEndpoints_AllowOnlyStaffAndAdmin()
    {
        var mutation = typeof(TicketsController).GetMethod(nameof(TicketsController.UpdateClassification));
        var lookup = Assert.Single(typeof(TaxonomyController).GetCustomAttributes<AuthorizeAttribute>());

        Assert.NotNull(mutation);
        Assert.Equal("TechnicalStaff,Admin", Assert.Single(mutation!.GetCustomAttributes<AuthorizeAttribute>()).Roles);
        Assert.Equal("TechnicalStaff,Admin", lookup.Roles);
        Assert.Empty(typeof(TaxonomyController).GetMethods()
            .SelectMany(method => method.GetCustomAttributes<AllowAnonymousAttribute>()));
    }

    [Fact]
    public async Task UpdateClassification_AssignsActiveValuesAndPreservesTicketLifecycleFields()
    {
        await using var harness = await TicketServiceTests.TicketServiceHarness.CreateAsync(new FixedTimeProvider(Now));
        var category = await AddCategoryAsync(harness, "Ağ");
        var tagA = await AddTagAsync(harness, "VPN");
        var tagB = await AddTagAsync(harness, "Erişim");
        harness.Ticket.FirstResponseDueAt = Now.AddHours(2);
        harness.Ticket.FirstRespondedAt = Now.AddMinutes(-5);
        harness.Ticket.ResolutionCycleStartedAt = Now.AddHours(-1);
        harness.Ticket.ResolutionDueAt = Now.AddHours(8);
        harness.Ticket.ResolvedAt = null;
        var original = (harness.Ticket.Status, harness.Ticket.Priority, harness.Ticket.AssignedUserId,
            harness.Ticket.CustomerId, harness.Ticket.ExternalRequesterId, harness.Ticket.CreatedAt,
            harness.Ticket.FirstResponseDueAt, harness.Ticket.FirstRespondedAt,
            harness.Ticket.ResolutionCycleStartedAt, harness.Ticket.ResolutionDueAt, harness.Ticket.ResolvedAt);
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.UpdateClassificationAsync(
            harness.Ticket.Id,
            harness.Admin.Id,
            UserRole.Admin,
            new UpdateTicketClassificationRequest { CategoryId = category.Id, TagIds = [tagB.Id, tagA.Id] });

        var ticket = await harness.DbContext.Tickets.SingleAsync();
        Assert.Equal(TicketOperationFailure.None, result.Failure);
        Assert.Equal(category.Id, ticket.CategoryId);
        Assert.Equal(new[] { tagB.Name, tagA.Name }.Order().ToArray(), result.Value!.Tags.Select(tag => tag.Name).ToArray());
        Assert.Equal(Now, ticket.UpdatedAt);
        Assert.Equal(original, (ticket.Status, ticket.Priority, ticket.AssignedUserId,
            ticket.CustomerId, ticket.ExternalRequesterId, ticket.CreatedAt,
            ticket.FirstResponseDueAt, ticket.FirstRespondedAt,
            ticket.ResolutionCycleStartedAt, ticket.ResolutionDueAt, ticket.ResolvedAt));
        Assert.Empty(await harness.DbContext.TicketMessages.ToListAsync());
    }

    [Fact]
    public async Task UpdateClassification_TechnicalStaffAllowedAndCustomerForbidden()
    {
        await using var harness = await TicketServiceTests.TicketServiceHarness.CreateAsync();
        var category = await AddCategoryAsync(harness, "Donanım");

        var technicalResult = await harness.Service.UpdateClassificationAsync(
            harness.Ticket.Id, harness.TechnicalStaff.Id, UserRole.TechnicalStaff,
            new UpdateTicketClassificationRequest { CategoryId = category.Id });
        var customerResult = await harness.Service.UpdateClassificationAsync(
            harness.Ticket.Id, harness.Customer.Id, UserRole.Customer,
            new UpdateTicketClassificationRequest { CategoryId = null });

        Assert.Equal(TicketOperationFailure.None, technicalResult.Failure);
        Assert.Equal(TicketOperationFailure.Forbidden, customerResult.Failure);
        Assert.Equal(category.Id, (await harness.DbContext.Tickets.SingleAsync()).CategoryId);
    }

    [Fact]
    public async Task UpdateClassification_UsesFullReplacementDeduplicatesAndIsIdempotent()
    {
        await using var harness = await TicketServiceTests.TicketServiceHarness.CreateAsync(new FixedTimeProvider(Now));
        var tagA = await AddTagAsync(harness, "A");
        var tagB = await AddTagAsync(harness, "B");
        var tagC = await AddTagAsync(harness, "C");
        harness.DbContext.TicketTagAssignments.AddRange(
            new TicketTagAssignment { TicketId = harness.Ticket.Id, TagId = tagA.Id },
            new TicketTagAssignment { TicketId = harness.Ticket.Id, TagId = tagB.Id });
        await harness.DbContext.SaveChangesAsync();
        var originalUpdatedAt = harness.Ticket.UpdatedAt;

        var replaced = await harness.Service.UpdateClassificationAsync(
            harness.Ticket.Id, harness.Admin.Id, UserRole.Admin,
            new UpdateTicketClassificationRequest { TagIds = [tagB.Id, tagC.Id, tagC.Id] });
        var noOp = await harness.Service.UpdateClassificationAsync(
            harness.Ticket.Id, harness.Admin.Id, UserRole.Admin,
            new UpdateTicketClassificationRequest { TagIds = [tagC.Id, tagB.Id] });

        var assignments = await harness.DbContext.TicketTagAssignments
            .Where(item => item.TicketId == harness.Ticket.Id)
            .OrderBy(item => item.TagId)
            .ToListAsync();
        var ticket = await harness.DbContext.Tickets.SingleAsync();
        Assert.Equal(TicketOperationFailure.None, replaced.Failure);
        Assert.Equal(TicketOperationFailure.None, noOp.Failure);
        Assert.Equal([tagB.Id, tagC.Id], assignments.Select(item => item.TagId).ToArray());
        Assert.Equal(Now, ticket.UpdatedAt);
        Assert.NotEqual(originalUpdatedAt, ticket.UpdatedAt);
    }

    [Fact]
    public async Task UpdateClassification_NoOpDoesNotChangeUpdatedAtOrCreateAssignments()
    {
        var firstMutationAt = Now;
        await using var harness = await TicketServiceTests.TicketServiceHarness.CreateAsync(
            new AdvancingTimeProvider(firstMutationAt));
        var tag = await AddTagAsync(harness, "Stable");

        var changed = await harness.Service.UpdateClassificationAsync(
            harness.Ticket.Id, harness.Admin.Id, UserRole.Admin,
            new UpdateTicketClassificationRequest { TagIds = [tag.Id] });
        var unchanged = await harness.Service.UpdateClassificationAsync(
            harness.Ticket.Id, harness.Admin.Id, UserRole.Admin,
            new UpdateTicketClassificationRequest { TagIds = [tag.Id] });

        var ticket = await harness.DbContext.Tickets.SingleAsync();
        Assert.Equal(TicketOperationFailure.None, changed.Failure);
        Assert.Equal(TicketOperationFailure.None, unchanged.Failure);
        Assert.Equal(firstMutationAt, ticket.UpdatedAt);
        Assert.Equal(1, await harness.DbContext.TicketTagAssignments.CountAsync());
    }

    [Fact]
    public async Task UpdateClassification_RejectsInvalidValuesAtomically()
    {
        await using var harness = await TicketServiceTests.TicketServiceHarness.CreateAsync();
        var category = await AddCategoryAsync(harness, "Current");
        var activeTag = await AddTagAsync(harness, "CurrentTag");
        var inactiveTag = await AddTagAsync(harness, "Inactive", false);
        harness.Ticket.CategoryId = category.Id;
        harness.DbContext.TicketTagAssignments.Add(new TicketTagAssignment { TicketId = harness.Ticket.Id, TagId = activeTag.Id });
        await harness.DbContext.SaveChangesAsync();
        var snapshot = (harness.Ticket.CategoryId, harness.Ticket.UpdatedAt);

        var result = await harness.Service.UpdateClassificationAsync(
            harness.Ticket.Id, harness.Admin.Id, UserRole.Admin,
            new UpdateTicketClassificationRequest { CategoryId = null, TagIds = [inactiveTag.Id] });

        var ticket = await harness.DbContext.Tickets.SingleAsync();
        Assert.Equal(TicketOperationFailure.InvalidRequest, result.Failure);
        Assert.Equal(snapshot, (ticket.CategoryId, ticket.UpdatedAt));
        Assert.Equal([activeTag.Id], (await harness.DbContext.TicketTagAssignments
            .Where(item => item.TicketId == harness.Ticket.Id)
            .Select(item => item.TagId).ToListAsync()).ToArray());
    }

    [Fact]
    public async Task UpdateClassification_PreservesOrRemovesInactiveHistoricalValuesWithoutNewAssignment()
    {
        await using var harness = await TicketServiceTests.TicketServiceHarness.CreateAsync();
        var inactiveCategory = await AddCategoryAsync(harness, "Legacy", false);
        var inactiveTag = await AddTagAsync(harness, "LegacyTag", false);
        harness.Ticket.CategoryId = inactiveCategory.Id;
        harness.DbContext.TicketTagAssignments.Add(new TicketTagAssignment { TicketId = harness.Ticket.Id, TagId = inactiveTag.Id });
        await harness.DbContext.SaveChangesAsync();

        var preserved = await harness.Service.UpdateClassificationAsync(
            harness.Ticket.Id, harness.Admin.Id, UserRole.Admin,
            new UpdateTicketClassificationRequest { CategoryId = inactiveCategory.Id, TagIds = [inactiveTag.Id] });
        var removed = await harness.Service.UpdateClassificationAsync(
            harness.Ticket.Id, harness.Admin.Id, UserRole.Admin,
            new UpdateTicketClassificationRequest { CategoryId = null, TagIds = [] });

        Assert.Equal(TicketOperationFailure.None, preserved.Failure);
        Assert.Equal(TicketOperationFailure.None, removed.Failure);
        Assert.Null((await harness.DbContext.Tickets.SingleAsync()).CategoryId);
        Assert.Empty(await harness.DbContext.TicketTagAssignments.ToListAsync());
    }

    [Fact]
    public async Task TicketProjections_ShowCategoryToCustomerButHideTags()
    {
        await using var harness = await TicketServiceTests.TicketServiceHarness.CreateAsync();
        var category = await AddCategoryAsync(harness, "Ağ", false);
        var tag = await AddTagAsync(harness, "Dahili", false);
        harness.Ticket.CategoryId = category.Id;
        harness.DbContext.TicketTagAssignments.Add(new TicketTagAssignment { TicketId = harness.Ticket.Id, TagId = tag.Id });
        await harness.DbContext.SaveChangesAsync();

        var customerDetail = await harness.Service.GetByIdAsync(harness.Ticket.Id, harness.Customer.Id, UserRole.Customer);
        var customerList = await harness.Service.GetAllAsync(harness.Customer.Id, UserRole.Customer);
        var staffDetail = await harness.Service.GetByIdAsync(harness.Ticket.Id, harness.Admin.Id, UserRole.Admin);

        Assert.Equal(category.Name, customerDetail!.Category!.Name);
        Assert.Empty(customerDetail.Tags);
        Assert.Empty(Assert.Single(customerList.Items).Tags);
        Assert.Equal(tag.Name, Assert.Single(staffDetail!.Tags).Name);
    }

    [Fact]
    public async Task TicketListFilters_ApplyCategoryTagUnclassifiedPaginationAndAgingBeforeCounting()
    {
        await using var harness = await TicketServiceTests.TicketServiceHarness.CreateAsync(new FixedTimeProvider(Now));
        var categoryA = await AddCategoryAsync(harness, "A");
        var categoryB = await AddCategoryAsync(harness, "B", false);
        var tagA = await AddTagAsync(harness, "TagA", false);
        var tagB = await AddTagAsync(harness, "TagB", false);
        harness.Ticket.CategoryId = categoryA.Id;
        harness.Ticket.CreatedAt = Now.AddHours(-9);
        harness.DbContext.Tickets.AddRange(
            new Ticket { TicketNumber = "SUP-CAT-B", CustomerId = harness.Customer.Id, CategoryId = categoryB.Id, Title = "Other", Description = "d", Status = TicketStatus.New, Priority = TicketPriority.High, CreatedAt = Now.AddHours(-9), UpdatedAt = Now },
            new Ticket { TicketNumber = "SUP-NONE", CustomerId = harness.Customer.Id, Title = "Unclassified", Description = "d", Status = TicketStatus.New, Priority = TicketPriority.Normal, CreatedAt = Now, UpdatedAt = Now });
        harness.DbContext.TicketTagAssignments.AddRange(
            new TicketTagAssignment { TicketId = harness.Ticket.Id, TagId = tagA.Id });
        await harness.DbContext.SaveChangesAsync();
        var categoryBTicket = await harness.DbContext.Tickets.SingleAsync(ticket => ticket.TicketNumber == "SUP-CAT-B");
        harness.DbContext.TicketTagAssignments.Add(new TicketTagAssignment { TicketId = categoryBTicket.Id, TagId = tagB.Id });
        await harness.DbContext.SaveChangesAsync();

        var categoryTag = await harness.Service.GetAllAsync(harness.Admin.Id, UserRole.Admin,
            categoryId: categoryA.Id, tagId: tagA.Id, page: 1, pageSize: 1);
        var inactiveHistorical = await harness.Service.GetAllAsync(harness.Admin.Id, UserRole.Admin, categoryId: categoryB.Id, tagId: tagB.Id);
        var unclassified = await harness.Service.GetAllAsync(harness.Admin.Id, UserRole.Admin,
            classificationState: TicketClassificationFilter.Unclassified);
        var aging = await harness.Service.GetAllAsync(harness.Admin.Id, UserRole.Admin,
            tagId: tagA.Id, agingFilter: TicketAgingFilter.WaitingOver8Hours,
            sort: TicketListSort.LongestWaiting);

        Assert.Equal(1, categoryTag.Pagination.TotalCount);
        Assert.Equal(harness.Ticket.Id, Assert.Single(categoryTag.Items).Id);
        Assert.Equal(categoryBTicket.Id, Assert.Single(inactiveHistorical.Items).Id);
        Assert.Contains(unclassified.Items, item => item.TicketNumber == "SUP-NONE");
        Assert.Equal(harness.Ticket.Id, Assert.Single(aging.Items).Id);
    }

    [Fact]
    public async Task TaxonomyLookup_ReturnsOnlyActiveAssignableValuesByDefault()
    {
        await using var harness = await TicketServiceTests.TicketServiceHarness.CreateAsync();
        var service = new TicketTaxonomyService(harness.DbContext, TimeProvider.System);
        var activeCategory = await AddCategoryAsync(harness, "Active");
        await AddCategoryAsync(harness, "Inactive", false);
        var activeTag = await AddTagAsync(harness, "ActiveTag");
        await AddTagAsync(harness, "InactiveTag", false);

        var categories = await service.GetClassificationCategoriesAsync(true);
        var tags = await service.GetClassificationTagsAsync(true);

        Assert.Equal([activeCategory.Id], categories.Select(item => item.Id).ToArray());
        Assert.Equal([activeTag.Id], tags.Select(item => item.Id).ToArray());
    }

    [Fact]
    public async Task NewPortalAndGmailTicketRecordsRemainUnclassified()
    {
        await using var harness = await TicketServiceTests.TicketServiceHarness.CreateAsync();
        var portal = await harness.Service.CreateAsync(harness.Customer.Id, new CreateTicketRequest
        {
            Title = "Portal", Description = "d", Priority = TicketPriority.Normal
        });
        var gmail = await harness.Service.CreateTicketRecordAsync(
            harness.Customer, "Gmail", "d", TicketPriority.Normal, Now, CancellationToken.None);

        Assert.Null(portal!.Category);
        Assert.Empty(portal.Tags);
        Assert.Null(gmail.CategoryId);
        Assert.Empty(await harness.DbContext.TicketTagAssignments
            .Where(item => item.TicketId == gmail.Id).ToListAsync());
    }

    private static async Task<TicketCategory> AddCategoryAsync(
        TicketServiceTests.TicketServiceHarness harness,
        string name,
        bool isActive = true)
    {
        var category = new TicketCategory
        {
            Name = name,
            NormalizedName = TaxonomyNameNormalizer.Normalize(name),
            IsActive = isActive,
            CreatedAt = Now
        };
        harness.DbContext.TicketCategories.Add(category);
        await harness.DbContext.SaveChangesAsync();
        return category;
    }

    private static async Task<TicketTag> AddTagAsync(
        TicketServiceTests.TicketServiceHarness harness,
        string name,
        bool isActive = true)
    {
        var tag = new TicketTag
        {
            Name = name,
            NormalizedName = TaxonomyNameNormalizer.Normalize(name),
            IsActive = isActive,
            CreatedAt = Now
        };
        harness.DbContext.TicketTags.Add(tag);
        await harness.DbContext.SaveChangesAsync();
        return tag;
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    private sealed class AdvancingTimeProvider(DateTime first) : TimeProvider
    {
        private DateTime _next = first;

        public override DateTimeOffset GetUtcNow()
        {
            var current = _next;
            _next = _next.AddMinutes(1);
            return new DateTimeOffset(current);
        }
    }
}
