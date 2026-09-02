using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using SupportFlow.Api.Data;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services.TicketActivities;

namespace SupportFlow.Api.Tests.TicketActivities;

public sealed class TicketActivityWriterTests
{
    private static readonly DateTime OccurredAt = new(2026, 8, 25, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime RecordedAt = new(2026, 8, 25, 9, 1, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Append_StagesValidUserActorAndSnapshotsWithoutSaving()
    {
        await using var harness = await ActivityHarness.CreateAsync();

        var activity = harness.Writer.Append(CreateUserRequest(harness.Ticket.Id, harness.User.Id));

        Assert.Equal(EntityState.Added, harness.DbContext.Entry(activity).State);
        Assert.Equal(RecordedAt, activity.RecordedAt);
        Assert.Equal("Ada Yilmaz", activity.ActorDisplayName);
        Assert.Equal(UserRole.TechnicalStaff, activity.ActorRole);
        Assert.Equal(0, await harness.CreateContext().TicketActivities.CountAsync());

        await harness.DbContext.SaveChangesAsync();

        Assert.Equal(1, await harness.CreateContext().TicketActivities.CountAsync());
    }

    [Fact]
    public async Task Append_StagesValidExternalRequesterActor()
    {
        await using var harness = await ActivityHarness.CreateAsync();

        var activity = harness.Writer.Append(new TicketActivityAppendRequest(
            harness.Ticket.Id, TicketActivityType.TicketCreated, TicketActivitySource.GmailInbound,
            TicketActivityVisibility.RequesterVisible, OccurredAt, TicketActivityActorKind.ExternalRequester,
            null, harness.ExternalRequester.Id, "External Requester"));

        await harness.DbContext.SaveChangesAsync();

        Assert.Equal(harness.ExternalRequester.Id, activity.ExternalRequesterId);
        Assert.Null(activity.ActorUserId);
        Assert.Null(activity.ActorRole);
    }

    [Fact]
    public async Task Append_StagesValidSystemActorWithNullableRole()
    {
        await using var harness = await ActivityHarness.CreateAsync();

        var activity = harness.Writer.Append(new TicketActivityAppendRequest(
            harness.Ticket.Id, TicketActivityType.TicketCreated, TicketActivitySource.System,
            TicketActivityVisibility.Internal, OccurredAt, TicketActivityActorKind.System,
            null, null, "Sistem"));

        await harness.DbContext.SaveChangesAsync();

        Assert.Null(activity.ActorUserId);
        Assert.Null(activity.ExternalRequesterId);
        Assert.Null(activity.ActorRole);
    }

    [Theory]
    [MemberData(nameof(InvalidActorRequests))]
    public async Task Append_RejectsInconsistentActorContracts(TicketActivityAppendRequest request)
    {
        await using var harness = await ActivityHarness.CreateAsync();
        var contextualRequest = request with { TicketId = harness.Ticket.Id };

        Assert.Throws<ArgumentException>(() => harness.Writer.Append(contextualRequest));
        Assert.Empty(harness.DbContext.ChangeTracker.Entries<TicketActivity>());
    }

    [Fact]
    public async Task Append_RejectsDefaultAndNonUtcOccurredAt()
    {
        await using var harness = await ActivityHarness.CreateAsync();

        Assert.Throws<ArgumentException>(() => harness.Writer.Append(CreateUserRequest(harness.Ticket.Id, harness.User.Id) with { OccurredAt = default }));
        Assert.Throws<ArgumentException>(() => harness.Writer.Append(CreateUserRequest(harness.Ticket.Id, harness.User.Id) with { OccurredAt = DateTime.SpecifyKind(OccurredAt, DateTimeKind.Local) }));
    }

    [Fact]
    public void MetadataCodec_SerializesAndDeserializesTypedV1Contracts()
    {
        var codec = new TicketActivityMetadataCodec();
        var cases = new (TicketActivityType Type, TicketActivityMetadata Metadata)[]
        {
            (TicketActivityType.StatusChanged, new StatusChangedTicketActivityMetadata(TicketStatus.New, TicketStatus.Assigned)),
            (TicketActivityType.PriorityChanged, new PriorityChangedTicketActivityMetadata(TicketPriority.Normal, TicketPriority.High)),
            (TicketActivityType.AssignmentChanged, new AssignmentChangedTicketActivityMetadata(null, 4, null, "Ada Yilmaz")),
            (TicketActivityType.ClassificationChanged, new ClassificationChangedTicketActivityMetadata(
                null, 3, null, "Network", [new TicketActivityTagSnapshot(7, "VPN")], []))
        };

        foreach (var item in cases)
        {
            var serialized = codec.Serialize(item.Type, item.Metadata);
            var deserialized = codec.Deserialize(item.Type, serialized.Version, serialized.Json);

            Assert.Equal(TicketActivityMetadataCodec.CurrentMetadataVersion, serialized.Version);
            Assert.Equal(item.Metadata.GetType(), deserialized!.GetType());
            Assert.Equal(serialized.Json, codec.Serialize(item.Type, deserialized).Json);
        }
    }

    [Fact]
    public void MetadataCodec_RejectsUnsupportedMetadataAndMessageBodies()
    {
        var codec = new TicketActivityMetadataCodec();

        Assert.Throws<ArgumentException>(() => codec.Serialize(
            TicketActivityType.MessageCreated,
            new StatusChangedTicketActivityMetadata(TicketStatus.New, TicketStatus.Assigned)));
        Assert.Throws<ArgumentException>(() => codec.Deserialize(
            TicketActivityType.MessageCreated,
            TicketActivityMetadataCodec.CurrentMetadataVersion,
            "{\"message\":\"sensitive\"}"));
        Assert.DoesNotContain(typeof(TicketActivityMetadata).Assembly.GetTypes()
            .Where(type => typeof(TicketActivityMetadata).IsAssignableFrom(type))
            .SelectMany(type => type.GetProperties()), property => property.Name.Equals("Message", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Append_AllowsMultipleActivitiesInCallerTransaction()
    {
        await using var harness = await ActivityHarness.CreateAsync();

        await using var transaction = await harness.DbContext.Database.BeginTransactionAsync();
        harness.Writer.Append(CreateUserRequest(harness.Ticket.Id, harness.User.Id));
        harness.Writer.Append(CreateUserRequest(harness.Ticket.Id, harness.User.Id) with
        {
            ActivityType = TicketActivityType.PriorityChanged,
            Metadata = new PriorityChangedTicketActivityMetadata(TicketPriority.Normal, TicketPriority.High)
        });
        await harness.DbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        Assert.Equal(2, await harness.CreateContext().TicketActivities.CountAsync());
    }

    [Fact]
    public async Task Model_UsesNoActionRelationshipsRequiredIndexesAndCheckConstraints()
    {
        await using var harness = await ActivityHarness.CreateAsync();
        var entityType = harness.DbContext.GetService<IDesignTimeModel>()
            .Model.FindEntityType(typeof(TicketActivity))!;

        Assert.All(entityType.GetForeignKeys(), foreignKey => Assert.Equal(DeleteBehavior.NoAction, foreignKey.DeleteBehavior));
        Assert.Contains(entityType.GetIndexes(), index => IndexMatches(index, nameof(TicketActivity.TicketId), nameof(TicketActivity.OccurredAt), nameof(TicketActivity.Id)));
        Assert.Contains(entityType.GetIndexes(), index => IndexMatches(index, nameof(TicketActivity.ActorUserId), nameof(TicketActivity.OccurredAt)));
        Assert.Contains(entityType.GetIndexes(), index => IndexMatches(index, nameof(TicketActivity.ExternalRequesterId), nameof(TicketActivity.OccurredAt)));
        Assert.Contains(entityType.GetIndexes(), index => IndexMatches(index, nameof(TicketActivity.ActivityType), nameof(TicketActivity.OccurredAt)));
        Assert.Contains(entityType.GetCheckConstraints(), constraint => constraint.Name == "CK_TicketActivities_ActorKind");
        Assert.Contains(entityType.GetCheckConstraints(), constraint => constraint.Name == "CK_TicketActivities_MetadataVersion");
    }

    [Fact]
    public async Task LegacyCanonicalRecords_DoNotRequireActivityRows()
    {
        await using var harness = await ActivityHarness.CreateAsync();
        harness.DbContext.TicketMessages.Add(new TicketMessage
        {
            TicketId = harness.Ticket.Id,
            SenderId = harness.User.Id,
            Message = "Legacy message",
            CreatedAt = OccurredAt
        });
        harness.DbContext.TicketStatusHistories.Add(new TicketStatusHistory
        {
            TicketId = harness.Ticket.Id,
            ChangedByUserId = harness.User.Id,
            OldStatus = TicketStatus.New,
            NewStatus = TicketStatus.Assigned,
            ChangedAt = OccurredAt
        });
        await harness.DbContext.SaveChangesAsync();

        Assert.Empty(await harness.DbContext.TicketActivities.ToListAsync());
        Assert.Single(await harness.DbContext.TicketMessages.ToListAsync());
        Assert.Single(await harness.DbContext.TicketStatusHistories.ToListAsync());
    }

    [Fact]
    public void Writer_IsAppendOnlyAndHasNoMutationMethods()
    {
        var methods = typeof(ITicketActivityWriter).GetMethods(BindingFlags.Instance | BindingFlags.Public);

        Assert.Equal([nameof(ITicketActivityWriter.Append)], methods.Select(method => method.Name).ToArray());
        Assert.DoesNotContain("TicketActivitiesController", typeof(TicketActivityWriter).Assembly.GetTypes().Select(type => type.Name));
    }

    public static IEnumerable<object[]> InvalidActorRequests()
    {
        yield return [CreateInvalidRequest(TicketActivityActorKind.User, null, null, null)];
        yield return [CreateInvalidRequest(TicketActivityActorKind.User, 1, 2, UserRole.Admin)];
        yield return [CreateInvalidRequest(TicketActivityActorKind.ExternalRequester, null, null, null)];
        yield return [CreateInvalidRequest(TicketActivityActorKind.ExternalRequester, 1, 2, null)];
        yield return [CreateInvalidRequest(TicketActivityActorKind.System, 1, null, null)];
        yield return [CreateInvalidRequest(TicketActivityActorKind.System, null, 2, null)];
    }

    private static TicketActivityAppendRequest CreateUserRequest(int ticketId, int userId) =>
        new(ticketId, TicketActivityType.TicketCreated, TicketActivitySource.Portal,
            TicketActivityVisibility.RequesterVisible, OccurredAt, TicketActivityActorKind.User,
            userId, null, "Ada Yilmaz", UserRole.TechnicalStaff);

    private static TicketActivityAppendRequest CreateInvalidRequest(
        TicketActivityActorKind actorKind,
        int? actorUserId,
        int? externalRequesterId,
        UserRole? actorRole) =>
        new(1, TicketActivityType.TicketCreated, TicketActivitySource.System,
            TicketActivityVisibility.Internal, OccurredAt, actorKind,
            actorUserId, externalRequesterId, "Sistem", actorRole);

    private static bool IndexMatches(IIndex index, params string[] propertyNames) =>
        index.Properties.Select(property => property.Name).SequenceEqual(propertyNames);

    private sealed class ActivityHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        private ActivityHarness(SqliteConnection connection, DbContextOptions<AppDbContext> options, AppDbContext dbContext)
        {
            _connection = connection;
            _options = options;
            DbContext = dbContext;
            Writer = new TicketActivityWriter(dbContext, new TicketActivityMetadataCodec(), new FixedTimeProvider(RecordedAt));
        }

        public AppDbContext DbContext { get; }
        public TicketActivityWriter Writer { get; }
        public Ticket Ticket { get; private set; } = null!;
        public User User { get; private set; } = null!;
        public ExternalRequester ExternalRequester { get; private set; } = null!;

        public static async Task<ActivityHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            var dbContext = new AppDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            var harness = new ActivityHarness(connection, options, dbContext);
            harness.User = new User
            {
                FirstName = "Ada",
                LastName = "Yilmaz",
                Email = "ada@example.test",
                PasswordHash = "hash",
                Role = UserRole.TechnicalStaff,
                CreatedAt = OccurredAt
            };
            harness.ExternalRequester = new ExternalRequester
            {
                Email = "external@example.test",
                NormalizedEmail = "external@example.test",
                DisplayName = "External Requester",
                CreatedAt = OccurredAt,
                UpdatedAt = OccurredAt
            };
            dbContext.Users.Add(harness.User);
            dbContext.ExternalRequesters.Add(harness.ExternalRequester);
            await dbContext.SaveChangesAsync();

            harness.Ticket = new Ticket
            {
                TicketNumber = "SUP-TEST-000001",
                Title = "Activity test",
                Description = "Activity test",
                Status = TicketStatus.New,
                Priority = TicketPriority.Normal,
                CreatedAt = OccurredAt,
                UpdatedAt = OccurredAt
            };
            dbContext.Tickets.Add(harness.Ticket);
            await dbContext.SaveChangesAsync();
            return harness;
        }

        public AppDbContext CreateContext() => new(_options);

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
