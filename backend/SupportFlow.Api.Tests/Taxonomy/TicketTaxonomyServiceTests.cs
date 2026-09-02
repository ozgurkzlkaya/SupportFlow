using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportFlow.Api.Controllers;
using SupportFlow.Api.Data;
using SupportFlow.Api.DTOs.Taxonomy;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services;

namespace SupportFlow.Api.Tests.Taxonomy;

public sealed class TicketTaxonomyServiceTests
{
    private static readonly DateTime Now = new(2026, 8, 25, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AdminTaxonomyController_RequiresAdminOnly()
    {
        var attribute = Assert.Single(typeof(AdminTaxonomyController).GetCustomAttributes<AuthorizeAttribute>());

        Assert.Equal(nameof(UserRole.Admin), attribute.Roles);
        Assert.DoesNotContain(nameof(UserRole.TechnicalStaff), attribute.Roles);
        Assert.DoesNotContain(nameof(UserRole.Customer), attribute.Roles);
        Assert.Empty(typeof(AdminTaxonomyController).GetMethods()
            .SelectMany(method => method.GetCustomAttributes<AllowAnonymousAttribute>()));
    }

    [Fact]
    public async Task Categories_ListSupportsPagedSearchStatusAndUsageCount()
    {
        await using var harness = await TaxonomyHarness.CreateAsync();
        var active = await harness.AddCategoryAsync("Network", true);
        await harness.AddCategoryAsync("Hardware", false);
        await harness.AddTicketAsync(active.Id);

        var result = await harness.Service.GetCategoriesAsync("network", TaxonomyStatusFilter.Active, 1, 25);

        var item = Assert.Single(result.Items);
        Assert.Equal(active.Id, item.Id);
        Assert.Equal(1, item.UsageCount);
        Assert.Equal(1, result.Pagination.TotalCount);
        Assert.Equal(1, result.Pagination.Page);
        Assert.Equal(25, result.Pagination.PageSize);
    }

    [Fact]
    public async Task CreateCategory_TrimsNameNormalizesAndStartsActive()
    {
        await using var harness = await TaxonomyHarness.CreateAsync();

        var result = await harness.Service.CreateCategoryAsync(new CreateTicketCategoryRequest
        {
            Name = "  VPN Erişimi  ",
            Description = "  Ağ erişim talepleri  "
        });

        var response = Assert.IsType<SupportFlow.Api.DTOs.Taxonomy.TicketCategoryResponse>(result.Value);
        var persisted = await harness.DbContext.TicketCategories.SingleAsync();
        Assert.Equal(TaxonomyMutationFailure.None, result.Failure);
        Assert.Equal("VPN Erişimi", response.Name);
        Assert.Equal("Ağ erişim talepleri", response.Description);
        Assert.True(response.IsActive);
        Assert.Equal(TaxonomyNameNormalizer.Normalize("VPN Erişimi"), persisted.NormalizedName);
        Assert.Equal(Now, response.CreatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task CreateCategory_RejectsEmptyName(string name)
    {
        await using var harness = await TaxonomyHarness.CreateAsync();

        var result = await harness.Service.CreateCategoryAsync(new CreateTicketCategoryRequest { Name = name });

        Assert.Equal(TaxonomyMutationFailure.Invalid, result.Failure);
        Assert.Empty(await harness.DbContext.TicketCategories.ToListAsync());
    }

    [Theory]
    [InlineData("vpn")]
    [InlineData(" Vpn ")]
    [InlineData("VPN")]
    public async Task CreateCategory_RejectsNormalizedDuplicate(string duplicateName)
    {
        await using var harness = await TaxonomyHarness.CreateAsync();
        await harness.AddCategoryAsync("VPN");

        var result = await harness.Service.CreateCategoryAsync(new CreateTicketCategoryRequest { Name = duplicateName });

        Assert.Equal(TaxonomyMutationFailure.Duplicate, result.Failure);
        Assert.Equal(1, await harness.DbContext.TicketCategories.CountAsync());
    }

    [Fact]
    public async Task UpdateCategory_UpdatesNameDescriptionAndNormalization()
    {
        await using var harness = await TaxonomyHarness.CreateAsync();
        var category = await harness.AddCategoryAsync("Network", description: "Old");

        var result = await harness.Service.UpdateCategoryAsync(category.Id, new UpdateTicketCategoryRequest
        {
            Name = "  Ağ  ",
            Description = "  Yeni açıklama  "
        });

        var persisted = await harness.DbContext.TicketCategories.SingleAsync();
        Assert.Equal(TaxonomyMutationFailure.None, result.Failure);
        Assert.Equal("Ağ", persisted.Name);
        Assert.Equal("Yeni açıklama", persisted.Description);
        Assert.Equal(TaxonomyNameNormalizer.Normalize("Ağ"), persisted.NormalizedName);
        Assert.Equal(Now, persisted.UpdatedAt);
    }

    [Fact]
    public async Task UpdateCategory_RejectsDuplicateNormalizedName()
    {
        await using var harness = await TaxonomyHarness.CreateAsync();
        var network = await harness.AddCategoryAsync("Network");
        var hardware = await harness.AddCategoryAsync("Hardware");

        var result = await harness.Service.UpdateCategoryAsync(hardware.Id, new UpdateTicketCategoryRequest { Name = " network " });

        Assert.Equal(TaxonomyMutationFailure.Duplicate, result.Failure);
        Assert.Equal("Hardware", (await harness.DbContext.TicketCategories.SingleAsync(item => item.Id == hardware.Id)).Name);
        Assert.Equal("Network", (await harness.DbContext.TicketCategories.SingleAsync(item => item.Id == network.Id)).Name);
    }

    [Fact]
    public async Task CategoryStatus_IsIdempotentAndPreservesReferencedTicket()
    {
        await using var harness = await TaxonomyHarness.CreateAsync();
        var category = await harness.AddCategoryAsync("Network");
        var ticket = await harness.AddTicketAsync(category.Id);
        var originalTicket = (ticket.Status, ticket.Priority, ticket.CreatedAt, ticket.UpdatedAt, ticket.CategoryId);

        var deactivated = await harness.Service.UpdateCategoryStatusAsync(category.Id, false);
        var unchanged = await harness.Service.UpdateCategoryStatusAsync(category.Id, false);
        var reactivated = await harness.Service.UpdateCategoryStatusAsync(category.Id, true);
        var persistedTicket = await harness.DbContext.Tickets.SingleAsync(item => item.Id == ticket.Id);

        Assert.False(deactivated.Value!.IsActive);
        Assert.False(unchanged.Value!.IsActive);
        Assert.True(reactivated.Value!.IsActive);
        Assert.Equal(deactivated.Value.UpdatedAt, unchanged.Value.UpdatedAt);
        Assert.Equal(originalTicket, (persistedTicket.Status, persistedTicket.Priority, persistedTicket.CreatedAt, persistedTicket.UpdatedAt, persistedTicket.CategoryId));
    }

    [Fact]
    public async Task Tags_ListSupportsPagedSearchStatusAndUsageCount()
    {
        await using var harness = await TaxonomyHarness.CreateAsync();
        var active = await harness.AddTagAsync("VPN");
        await harness.AddTagAsync("Legacy", false);
        var ticket = await harness.AddTicketAsync();
        harness.DbContext.TicketTagAssignments.Add(new TicketTagAssignment { TicketId = ticket.Id, TagId = active.Id });
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Service.GetTagsAsync("vpn", TaxonomyStatusFilter.Active, 1, 25);

        var item = Assert.Single(result.Items);
        Assert.Equal(active.Id, item.Id);
        Assert.Equal(1, item.UsageCount);
        Assert.Equal(1, result.Pagination.TotalCount);
    }

    [Fact]
    public async Task CreateTag_TrimsNormalizesAndRejectsDuplicate()
    {
        await using var harness = await TaxonomyHarness.CreateAsync();

        var created = await harness.Service.CreateTagAsync(new CreateTicketTagRequest { Name = "  VPN  " });
        var duplicate = await harness.Service.CreateTagAsync(new CreateTicketTagRequest { Name = "vpn" });
        var persisted = await harness.DbContext.TicketTags.SingleAsync();

        Assert.Equal(TaxonomyMutationFailure.None, created.Failure);
        Assert.Equal("VPN", persisted.Name);
        Assert.Equal("VPN", persisted.NormalizedName);
        Assert.Equal(TaxonomyMutationFailure.Duplicate, duplicate.Failure);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateTag_RejectsEmptyName(string name)
    {
        await using var harness = await TaxonomyHarness.CreateAsync();

        var result = await harness.Service.CreateTagAsync(new CreateTicketTagRequest { Name = name });

        Assert.Equal(TaxonomyMutationFailure.Invalid, result.Failure);
        Assert.Empty(await harness.DbContext.TicketTags.ToListAsync());
    }

    [Fact]
    public async Task UpdateTag_UpdatesNormalizationAndRejectsDuplicateRename()
    {
        await using var harness = await TaxonomyHarness.CreateAsync();
        var vpn = await harness.AddTagAsync("VPN");
        var network = await harness.AddTagAsync("Network");

        var updated = await harness.Service.UpdateTagAsync(network.Id, new UpdateTicketTagRequest { Name = "  Ağ  " });
        var duplicate = await harness.Service.UpdateTagAsync(network.Id, new UpdateTicketTagRequest { Name = " vpn " });
        var persisted = await harness.DbContext.TicketTags.SingleAsync(item => item.Id == network.Id);

        Assert.Equal(TaxonomyMutationFailure.None, updated.Failure);
        Assert.Equal("Ağ", persisted.Name);
        Assert.Equal(TaxonomyNameNormalizer.Normalize("Ağ"), persisted.NormalizedName);
        Assert.Equal(TaxonomyMutationFailure.Duplicate, duplicate.Failure);
        Assert.Equal("VPN", (await harness.DbContext.TicketTags.SingleAsync(item => item.Id == vpn.Id)).Name);
    }

    [Fact]
    public async Task TagStatus_IsIdempotentAndPreservesHistoricalAssignment()
    {
        await using var harness = await TaxonomyHarness.CreateAsync();
        var tag = await harness.AddTagAsync("VPN");
        var ticket = await harness.AddTicketAsync();
        harness.DbContext.TicketTagAssignments.Add(new TicketTagAssignment { TicketId = ticket.Id, TagId = tag.Id });
        await harness.DbContext.SaveChangesAsync();

        var deactivated = await harness.Service.UpdateTagStatusAsync(tag.Id, false);
        var unchanged = await harness.Service.UpdateTagStatusAsync(tag.Id, false);
        var reactivated = await harness.Service.UpdateTagStatusAsync(tag.Id, true);

        Assert.False(deactivated.Value!.IsActive);
        Assert.Equal(deactivated.Value.UpdatedAt, unchanged.Value!.UpdatedAt);
        Assert.True(reactivated.Value!.IsActive);
        Assert.Equal(1, await harness.DbContext.TicketTagAssignments.CountAsync());
    }

    [Fact]
    public async Task DatabaseConfiguration_UsesNullableCategoryRestrictiveForeignKeysAndRequiredIndexes()
    {
        await using var harness = await TaxonomyHarness.CreateAsync();
        var ticketEntity = harness.DbContext.Model.FindEntityType(typeof(Ticket))!;
        var categoryProperty = ticketEntity.FindProperty(nameof(Ticket.CategoryId))!;
        var categoryForeignKey = ticketEntity.GetForeignKeys().Single(key => key.PrincipalEntityType.ClrType == typeof(TicketCategory));
        var assignmentEntity = harness.DbContext.Model.FindEntityType(typeof(TicketTagAssignment))!;
        var tagForeignKey = assignmentEntity.GetForeignKeys().Single(key => key.PrincipalEntityType.ClrType == typeof(TicketTag));

        Assert.True(categoryProperty.IsNullable);
        Assert.Equal(DeleteBehavior.NoAction, categoryForeignKey.DeleteBehavior);
        Assert.Equal(DeleteBehavior.NoAction, tagForeignKey.DeleteBehavior);
        Assert.Contains(ticketEntity.GetIndexes(), index => index.Properties.Single().Name == nameof(Ticket.CategoryId));
        Assert.Contains(assignmentEntity.GetIndexes(), index => index.Properties.Single().Name == nameof(TicketTagAssignment.TagId));
        Assert.Equal([nameof(TicketTagAssignment.TicketId), nameof(TicketTagAssignment.TagId)], assignmentEntity.FindPrimaryKey()!.Properties.Select(property => property.Name));
    }

    [Fact]
    public async Task DatabaseConstraint_RejectsDuplicateCategoryNormalizedName()
    {
        await using var harness = await TaxonomyHarness.CreateAsync();
        await harness.AddCategoryAsync("VPN");
        harness.DbContext.ChangeTracker.Clear();
        harness.DbContext.TicketCategories.Add(new TicketCategory
        {
            Name = "vpn",
            NormalizedName = TaxonomyNameNormalizer.Normalize("vpn"),
            IsActive = true,
            CreatedAt = Now
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => harness.DbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task DatabaseConstraint_RejectsDuplicateTagNormalizedName()
    {
        await using var harness = await TaxonomyHarness.CreateAsync();
        await harness.AddTagAsync("VPN");
        harness.DbContext.ChangeTracker.Clear();
        harness.DbContext.TicketTags.Add(new TicketTag
        {
            Name = "vpn",
            NormalizedName = TaxonomyNameNormalizer.Normalize("vpn"),
            IsActive = true,
            CreatedAt = Now
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => harness.DbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task DatabaseConstraints_RejectDuplicateCategoryTagAndAssignment()
    {
        await using var harness = await TaxonomyHarness.CreateAsync();
        var category = await harness.AddCategoryAsync("Network");
        var tag = await harness.AddTagAsync("VPN");
        var ticket = await harness.AddTicketAsync(category.Id);
        harness.DbContext.TicketTagAssignments.Add(new TicketTagAssignment { TicketId = ticket.Id, TagId = tag.Id });
        await harness.DbContext.SaveChangesAsync();
        harness.DbContext.ChangeTracker.Clear();

        harness.DbContext.TicketTagAssignments.Add(new TicketTagAssignment { TicketId = ticket.Id, TagId = tag.Id });

        await Assert.ThrowsAsync<DbUpdateException>(() => harness.DbContext.SaveChangesAsync());
    }

    private sealed class TaxonomyHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TaxonomyHarness(SqliteConnection connection, AppDbContext dbContext, TicketTaxonomyService service)
        {
            _connection = connection;
            DbContext = dbContext;
            Service = service;
        }

        public AppDbContext DbContext { get; }
        public TicketTaxonomyService Service { get; }

        public static async Task<TaxonomyHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var dbContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options);
            await dbContext.Database.EnsureCreatedAsync();
            return new TaxonomyHarness(connection, dbContext, new TicketTaxonomyService(dbContext, new FixedTimeProvider(Now)));
        }

        public async Task<TicketCategory> AddCategoryAsync(string name, bool isActive = true, string? description = null)
        {
            var category = new TicketCategory
            {
                Name = name,
                NormalizedName = TaxonomyNameNormalizer.Normalize(name),
                Description = description,
                IsActive = isActive,
                CreatedAt = Now
            };
            DbContext.TicketCategories.Add(category);
            await DbContext.SaveChangesAsync();
            return category;
        }

        public async Task<TicketTag> AddTagAsync(string name, bool isActive = true)
        {
            var tag = new TicketTag
            {
                Name = name,
                NormalizedName = TaxonomyNameNormalizer.Normalize(name),
                IsActive = isActive,
                CreatedAt = Now
            };
            DbContext.TicketTags.Add(tag);
            await DbContext.SaveChangesAsync();
            return tag;
        }

        public async Task<Ticket> AddTicketAsync(int? categoryId = null)
        {
            var ticket = new Ticket
            {
                TicketNumber = $"SUP-{Guid.NewGuid():N}"[..30],
                CategoryId = categoryId,
                Title = "Taxonomy test",
                Description = "Taxonomy test ticket",
                Status = TicketStatus.New,
                Priority = TicketPriority.Normal,
                CreatedAt = Now,
                UpdatedAt = Now
            };
            DbContext.Tickets.Add(ticket);
            await DbContext.SaveChangesAsync();
            return ticket;
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}
