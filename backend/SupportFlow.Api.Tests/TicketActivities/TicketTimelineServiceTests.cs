using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SupportFlow.Api.DTOs.Common;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services;
using SupportFlow.Api.Services.TicketActivities;
using SupportFlow.Api.Tests.Tickets;

namespace SupportFlow.Api.Tests.TicketActivities;

public sealed class TicketTimelineServiceTests
{
    [Fact]
    public async Task Timeline_AllowsAdminTechnicalStaffAndOwningCustomer_ButHidesOtherCustomerTicket()
    {
        await using var harness = await TicketServiceTests.TicketServiceHarness.CreateAsync();
        var service = CreateService(harness);
        var otherCustomer = await AddCustomerAsync(harness, "other@example.test");
        var otherTicket = new Ticket
        {
            TicketNumber = "SUP-2026-OTHER",
            CustomerId = otherCustomer.Id,
            Title = "Other ticket",
            Description = "Description",
            CreatedAt = Utc(9, 0),
            UpdatedAt = Utc(9, 0)
        };
        harness.DbContext.Tickets.Add(otherTicket);
        await harness.DbContext.SaveChangesAsync();

        var admin = await service.GetTimelineAsync(harness.Ticket.Id, harness.Admin.Id, UserRole.Admin, 1, 25);
        var technical = await service.GetTimelineAsync(harness.Ticket.Id, harness.TechnicalStaff.Id, UserRole.TechnicalStaff, 1, 25);
        var owner = await service.GetTimelineAsync(harness.Ticket.Id, harness.Customer.Id, UserRole.Customer, 1, 25);
        var other = await service.GetTimelineAsync(harness.Ticket.Id, otherCustomer.Id, UserRole.Customer, 1, 25);

        Assert.Equal(TicketOperationFailure.None, admin.Failure);
        Assert.Equal(TicketOperationFailure.None, technical.Failure);
        Assert.Equal(TicketOperationFailure.None, owner.Failure);
        Assert.Equal(TicketOperationFailure.NotFound, other.Failure);
    }

    [Fact]
    public async Task LegacyTicket_ProjectsSyntheticCreationStatusMessageAndAttachmentExactlyOnce()
    {
        await using var harness = await TicketServiceTests.TicketServiceHarness.CreateAsync();
        var history = await AddStatusHistoryAsync(harness, Utc(9, 1));
        var message = await AddMessageAsync(harness, harness.Customer.Id, false, "Legacy public message", Utc(9, 2));
        var attachment = await AddAttachmentAsync(harness, message.Id, harness.Customer.Id, null, "legacy.png", Utc(9, 3));
        var service = CreateService(harness);

        var result = await service.GetTimelineAsync(harness.Ticket.Id, harness.Admin.Id, UserRole.Admin, 1, 25);

        var response = AssertSuccess(result);
        Assert.Equal(4, response.Pagination.TotalCount);
        Assert.Contains(response.Items, item => item.Id == "ticket-created-" + harness.Ticket.Id);
        Assert.Contains(response.Items, item => item.Id == "status-" + history.Id && item.Metadata!.NewStatus == TicketStatus.InProgress);
        Assert.Contains(response.Items, item => item.Id == "message-" + message.Id && item.RelatedMessageId == message.Id);
        var attachmentItem = Assert.Single(response.Items, item => item.Id == "attachment-" + attachment.Id);
        Assert.Equal("legacy.png", attachmentItem.Attachment!.FileName);
        Assert.DoesNotContain("uploads", attachmentItem.Attachment.FileName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RealTicketCreatedActivity_SuppressesSyntheticCreationFallback()
    {
        await using var harness = await TicketServiceTests.TicketServiceHarness.CreateAsync();
        await AppendActivityAsync(
            harness,
            TicketActivityType.TicketCreated,
            TicketActivityVisibility.RequesterVisible,
            Utc(9, 0),
            TicketActivityActorKind.User,
            harness.Customer.Id,
            null,
            "Test Customer",
            UserRole.Customer);
        var service = CreateService(harness);

        var result = await service.GetTimelineAsync(harness.Ticket.Id, harness.Admin.Id, UserRole.Admin, 1, 25);

        var response = AssertSuccess(result);
        Assert.Single(response.Items);
        Assert.StartsWith("activity-", response.Items[0].Id, StringComparison.Ordinal);
        Assert.DoesNotContain(response.Items, item => item.Id == "ticket-created-" + harness.Ticket.Id);
    }

    [Fact]
    public async Task LinkedCanonicalRecords_AreSuppressedBeforePagination()
    {
        await using var harness = await TicketServiceTests.TicketServiceHarness.CreateAsync();
        var history = await AddStatusHistoryAsync(harness, Utc(9, 1));
        var message = await AddMessageAsync(harness, harness.Customer.Id, false, "Public body", Utc(9, 2));
        var attachment = await AddAttachmentAsync(harness, null, harness.Customer.Id, null, "linked.png", Utc(9, 3));
        await AppendActivityAsync(
            harness,
            TicketActivityType.StatusChanged,
            TicketActivityVisibility.RequesterVisible,
            history.ChangedAt,
            TicketActivityActorKind.User,
            harness.Admin.Id,
            null,
            "Admin User",
            UserRole.Admin,
            relatedStatusHistoryId: history.Id,
            metadata: new StatusChangedTicketActivityMetadata(history.OldStatus, history.NewStatus));
        await AppendActivityAsync(
            harness,
            TicketActivityType.MessageCreated,
            TicketActivityVisibility.RequesterVisible,
            message.CreatedAt,
            TicketActivityActorKind.User,
            harness.Customer.Id,
            null,
            "Test Customer",
            UserRole.Customer,
            relatedMessageId: message.Id);
        await AppendActivityAsync(
            harness,
            TicketActivityType.AttachmentAdded,
            TicketActivityVisibility.RequesterVisible,
            attachment.CreatedAt,
            TicketActivityActorKind.User,
            harness.Customer.Id,
            null,
            "Test Customer",
            UserRole.Customer,
            relatedAttachmentId: attachment.Id);
        var service = CreateService(harness);

        var result = await service.GetTimelineAsync(harness.Ticket.Id, harness.Admin.Id, UserRole.Admin, 1, 25);

        var response = AssertSuccess(result);
        Assert.Equal(4, response.Pagination.TotalCount);
        Assert.DoesNotContain(response.Items, item => item.Id == "status-" + history.Id);
        Assert.DoesNotContain(response.Items, item => item.Id == "message-" + message.Id);
        Assert.DoesNotContain(response.Items, item => item.Id == "attachment-" + attachment.Id);
        Assert.Equal(3, response.Items.Count(item => item.Id.StartsWith("activity-", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CustomerTimeline_OnlyContainsRequesterVisibleSafeEvents()
    {
        await using var harness = await TicketServiceTests.TicketServiceHarness.CreateAsync();
        var publicMessage = await AddMessageAsync(harness, harness.Customer.Id, false, "Visible public message", Utc(9, 1));
        await AddMessageAsync(harness, harness.TechnicalStaff.Id, true, "Hidden internal note", Utc(9, 2));
        var failedStaffMessage = await AddMessageAsync(harness, harness.TechnicalStaff.Id, false, "Failed public delivery", Utc(9, 2));
        failedStaffMessage.OutboundDeliveryStatus = OutboundDeliveryStatus.Failed;
        await harness.DbContext.SaveChangesAsync();
        var publicAttachment = await AddAttachmentAsync(harness, null, harness.Customer.Id, null, "visible.png", Utc(9, 3));
        var internalMessage = await AddMessageAsync(harness, harness.TechnicalStaff.Id, true, "Internal attachment parent", Utc(9, 4));
        await AddAttachmentAsync(harness, internalMessage.Id, harness.TechnicalStaff.Id, null, "hidden.txt", Utc(9, 5));
        await AppendActivityAsync(
            harness,
            TicketActivityType.MessageCreated,
            TicketActivityVisibility.RequesterVisible,
            failedStaffMessage.CreatedAt,
            TicketActivityActorKind.User,
            harness.TechnicalStaff.Id,
            null,
            "Tech User",
            UserRole.TechnicalStaff,
            relatedMessageId: failedStaffMessage.Id);
        await AppendActivityAsync(
            harness,
            TicketActivityType.PriorityChanged,
            TicketActivityVisibility.Internal,
            Utc(9, 6),
            TicketActivityActorKind.User,
            harness.Admin.Id,
            null,
            "Admin User",
            UserRole.Admin,
            metadata: new PriorityChangedTicketActivityMetadata(TicketPriority.Normal, TicketPriority.High));
        await AppendActivityAsync(
            harness,
            TicketActivityType.AssignmentChanged,
            TicketActivityVisibility.Internal,
            Utc(9, 7),
            TicketActivityActorKind.User,
            harness.Admin.Id,
            null,
            "Admin User",
            UserRole.Admin,
            metadata: new AssignmentChangedTicketActivityMetadata(null, harness.TechnicalStaff.Id, null, "Tech User"));
        await AppendActivityAsync(
            harness,
            TicketActivityType.ClassificationChanged,
            TicketActivityVisibility.Internal,
            Utc(9, 8),
            TicketActivityActorKind.User,
            harness.Admin.Id,
            null,
            "Admin User",
            UserRole.Admin,
            metadata: new ClassificationChangedTicketActivityMetadata(null, null, null, null, [new TicketActivityTagSnapshot(1, "Internal")], []));
        var service = CreateService(harness);

        var customerResult = await service.GetTimelineAsync(harness.Ticket.Id, harness.Customer.Id, UserRole.Customer, 1, 25);
        var staffResult = await service.GetTimelineAsync(harness.Ticket.Id, harness.TechnicalStaff.Id, UserRole.TechnicalStaff, 1, 25);

        var customer = AssertSuccess(customerResult);
        var staff = AssertSuccess(staffResult);
        Assert.All(customer.Items, item => Assert.Equal(TicketActivityVisibility.RequesterVisible, item.Visibility));
        Assert.Contains(customer.Items, item => item.RelatedMessageId == publicMessage.Id);
        Assert.Contains(customer.Items, item => item.RelatedAttachmentId == publicAttachment.Id);
        Assert.DoesNotContain(customer.Items, item => item.Kind == TicketActivityType.InternalNoteCreated);
        Assert.DoesNotContain(customer.Items, item => item.Kind is TicketActivityType.PriorityChanged or TicketActivityType.AssignmentChanged or TicketActivityType.ClassificationChanged);
        Assert.DoesNotContain(customer.Items, item => item.RelatedMessageId == failedStaffMessage.Id);
        Assert.DoesNotContain(customer.Items, item => item.Attachment?.FileName == "hidden.txt");
        Assert.Contains(staff.Items, item => item.Kind == TicketActivityType.InternalNoteCreated);
        Assert.Contains(staff.Items, item => item.Kind == TicketActivityType.PriorityChanged);
    }

    [Fact]
    public async Task Timeline_DoesNotExposeMessageBodiesOrAttachmentStoragePaths()
    {
        await using var harness = await TicketServiceTests.TicketServiceHarness.CreateAsync();
        var message = await AddMessageAsync(harness, harness.Customer.Id, false, "Do not leak this body", Utc(9, 1));
        var attachment = await AddAttachmentAsync(harness, null, harness.Customer.Id, null, "safe-name.png", Utc(9, 2));
        attachment.FilePath = "C:\\ticket-uploads\\secret-directory\\safe-name.png";
        attachment.StoredFileName = "private-storage-key.png";
        await harness.DbContext.SaveChangesAsync();
        await AppendActivityAsync(
            harness,
            TicketActivityType.MessageCreated,
            TicketActivityVisibility.RequesterVisible,
            message.CreatedAt,
            TicketActivityActorKind.User,
            harness.Customer.Id,
            null,
            "Test Customer",
            UserRole.Customer,
            relatedMessageId: message.Id);
        var service = CreateService(harness);

        var result = await service.GetTimelineAsync(harness.Ticket.Id, harness.Customer.Id, UserRole.Customer, 1, 25);
        var response = AssertSuccess(result);
        var serialized = System.Text.Json.JsonSerializer.Serialize(response);

        Assert.DoesNotContain("Do not leak this body", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-directory", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("private-storage-key", serialized, StringComparison.Ordinal);
        Assert.Contains(response.Items, item => item.RelatedAttachmentId == attachment.Id && item.Attachment!.FileName == "safe-name.png");
    }

    [Fact]
    public async Task Timeline_UsesStableCrossSourceOrderingAndPaginationWithoutAdjacentDuplicates()
    {
        await using var harness = await TicketServiceTests.TicketServiceHarness.CreateAsync();
        harness.Ticket.CreatedAt = Utc(8, 0);
        await harness.DbContext.SaveChangesAsync();
        var sameTime = Utc(10, 0);
        var history = await AddStatusHistoryAsync(harness, sameTime);
        var message = await AddMessageAsync(harness, harness.Customer.Id, false, "Same timestamp", sameTime);
        var attachment = await AddAttachmentAsync(harness, null, harness.Customer.Id, null, "same.png", sameTime);
        await AppendActivityAsync(
            harness,
            TicketActivityType.PriorityChanged,
            TicketActivityVisibility.Internal,
            sameTime,
            TicketActivityActorKind.User,
            harness.Admin.Id,
            null,
            "Admin User",
            UserRole.Admin,
            metadata: new PriorityChangedTicketActivityMetadata(TicketPriority.Normal, TicketPriority.High));
        var service = CreateService(harness);

        var first = AssertSuccess(await service.GetTimelineAsync(harness.Ticket.Id, harness.Admin.Id, UserRole.Admin, 1, 2));
        var second = AssertSuccess(await service.GetTimelineAsync(harness.Ticket.Id, harness.Admin.Id, UserRole.Admin, 2, 2));
        var outOfRange = AssertSuccess(await service.GetTimelineAsync(harness.Ticket.Id, harness.Admin.Id, UserRole.Admin, 4, 2));

        Assert.Equal(5, first.Pagination.TotalCount);
        Assert.Equal(["activity-1", "status-" + history.Id], first.Items.Select(item => item.Id));
        Assert.Equal(["message-" + message.Id, "attachment-" + attachment.Id], second.Items.Select(item => item.Id));
        Assert.Empty(first.Items.Select(item => item.Id).Intersect(second.Items.Select(item => item.Id)));
        Assert.Empty(outOfRange.Items);
        Assert.Equal(3, outOfRange.Pagination.TotalPages);
    }

    [Fact]
    public async Task Timeline_MapsExternalAndSystemActorsSafely()
    {
        await using var harness = await TicketServiceTests.TicketServiceHarness.CreateAsync();
        var external = new ExternalRequester
        {
            Email = "external@example.test",
            NormalizedEmail = "external@example.test",
            DisplayName = "Harici Talep Sahibi"
        };
        harness.DbContext.ExternalRequesters.Add(external);
        await harness.DbContext.SaveChangesAsync();
        await AppendActivityAsync(
            harness,
            TicketActivityType.MessageCreated,
            TicketActivityVisibility.RequesterVisible,
            Utc(9, 1),
            TicketActivityActorKind.ExternalRequester,
            null,
            external.Id,
            external.DisplayName!,
            null);
        await AppendActivityAsync(
            harness,
            TicketActivityType.StatusChanged,
            TicketActivityVisibility.RequesterVisible,
            Utc(9, 2),
            TicketActivityActorKind.System,
            null,
            null,
            "Ignored system label",
            null,
            metadata: new StatusChangedTicketActivityMetadata(TicketStatus.New, TicketStatus.InProgress));
        var service = CreateService(harness);

        var response = AssertSuccess(await service.GetTimelineAsync(harness.Ticket.Id, harness.Admin.Id, UserRole.Admin, 1, 25));

        Assert.Contains(response.Items, item => item.Actor.Kind == TicketActivityActorKind.ExternalRequester && item.Actor.DisplayName == "Harici Talep Sahibi");
        Assert.Contains(response.Items, item => item.Actor.Kind == TicketActivityActorKind.System && item.Actor.DisplayName == "Sistem");
    }

    [Fact]
    public async Task MalformedActivityMetadata_IsOmittedWithoutBreakingTimeline()
    {
        await using var harness = await TicketServiceTests.TicketServiceHarness.CreateAsync();
        var activity = await AppendActivityAsync(
            harness,
            TicketActivityType.StatusChanged,
            TicketActivityVisibility.RequesterVisible,
            Utc(9, 1),
            TicketActivityActorKind.User,
            harness.Admin.Id,
            null,
            "Admin User",
            UserRole.Admin,
            metadata: new StatusChangedTicketActivityMetadata(TicketStatus.New, TicketStatus.InProgress));
        await harness.DbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE TicketActivities SET MetadataJson = {"{invalid"} WHERE Id = {activity.Id}");
        harness.DbContext.ChangeTracker.Clear();
        var service = CreateService(harness);

        var response = AssertSuccess(await service.GetTimelineAsync(harness.Ticket.Id, harness.Admin.Id, UserRole.Admin, 1, 25));

        Assert.Null(Assert.Single(response.Items, item => item.Id == "activity-" + activity.Id).Metadata);
    }

    [Fact]
    public async Task TimelineGet_IsReadOnlyAndDoesNotBackfillActivities()
    {
        await using var harness = await TicketServiceTests.TicketServiceHarness.CreateAsync();
        await AddStatusHistoryAsync(harness, Utc(9, 1));
        await AddMessageAsync(harness, harness.Customer.Id, false, "Legacy", Utc(9, 2));
        await AddAttachmentAsync(harness, null, harness.Customer.Id, null, "legacy.png", Utc(9, 3));
        var before = await GetCanonicalCountsAsync(harness);
        var service = CreateService(harness);

        _ = await service.GetTimelineAsync(harness.Ticket.Id, harness.Admin.Id, UserRole.Admin, 1, 25);

        var after = await GetCanonicalCountsAsync(harness);
        Assert.Equal(before, after);
    }

    private static TicketTimelineService CreateService(TicketServiceTests.TicketServiceHarness harness) =>
        new(
            harness.DbContext,
            new TicketActivityMetadataCodec(),
            NullLogger<TicketTimelineService>.Instance);

    private static PagedResponse<SupportFlow.Api.DTOs.Tickets.TicketTimelineItemResponse> AssertSuccess(
        TicketOperationResult<PagedResponse<SupportFlow.Api.DTOs.Tickets.TicketTimelineItemResponse>> result)
    {
        Assert.Equal(TicketOperationFailure.None, result.Failure);
        return Assert.IsType<PagedResponse<SupportFlow.Api.DTOs.Tickets.TicketTimelineItemResponse>>(result.Value);
    }

    private static async Task<User> AddCustomerAsync(TicketServiceTests.TicketServiceHarness harness, string email)
    {
        var customer = new User
        {
            FirstName = "Other",
            LastName = "Customer",
            Email = email,
            PasswordHash = "hash",
            Role = UserRole.Customer,
            IsActive = true,
            CreatedAt = Utc(8, 0)
        };
        harness.DbContext.Users.Add(customer);
        await harness.DbContext.SaveChangesAsync();
        return customer;
    }

    private static async Task<TicketStatusHistory> AddStatusHistoryAsync(
        TicketServiceTests.TicketServiceHarness harness,
        DateTime changedAt)
    {
        var history = new TicketStatusHistory
        {
            TicketId = harness.Ticket.Id,
            ChangedByUserId = harness.TechnicalStaff.Id,
            OldStatus = TicketStatus.New,
            NewStatus = TicketStatus.InProgress,
            ChangedAt = changedAt
        };
        harness.DbContext.TicketStatusHistories.Add(history);
        await harness.DbContext.SaveChangesAsync();
        return history;
    }

    private static async Task<TicketMessage> AddMessageAsync(
        TicketServiceTests.TicketServiceHarness harness,
        int senderId,
        bool isInternal,
        string body,
        DateTime createdAt)
    {
        var message = new TicketMessage
        {
            TicketId = harness.Ticket.Id,
            SenderId = senderId,
            Message = body,
            IsInternal = isInternal,
            CreatedAt = createdAt
        };
        harness.DbContext.TicketMessages.Add(message);
        await harness.DbContext.SaveChangesAsync();
        return message;
    }

    private static async Task<Attachment> AddAttachmentAsync(
        TicketServiceTests.TicketServiceHarness harness,
        int? messageId,
        int? uploadedByUserId,
        int? uploadedByExternalRequesterId,
        string fileName,
        DateTime createdAt)
    {
        var attachment = new Attachment
        {
            TicketId = harness.Ticket.Id,
            MessageId = messageId,
            UploadedByUserId = uploadedByUserId,
            UploadedByExternalRequesterId = uploadedByExternalRequesterId,
            OriginalFileName = fileName,
            StoredFileName = "stored-" + fileName,
            FilePath = "C:\\ticket-uploads\\" + fileName,
            ContentType = "image/png",
            FileSize = 128,
            CreatedAt = createdAt
        };
        harness.DbContext.Attachments.Add(attachment);
        await harness.DbContext.SaveChangesAsync();
        return attachment;
    }

    private static async Task<TicketActivity> AppendActivityAsync(
        TicketServiceTests.TicketServiceHarness harness,
        TicketActivityType activityType,
        TicketActivityVisibility visibility,
        DateTime occurredAt,
        TicketActivityActorKind actorKind,
        int? actorUserId,
        int? externalRequesterId,
        string displayName,
        UserRole? actorRole,
        int? relatedStatusHistoryId = null,
        int? relatedMessageId = null,
        int? relatedAttachmentId = null,
        TicketActivityMetadata? metadata = null)
    {
        var writer = new TicketActivityWriter(
            harness.DbContext,
            new TicketActivityMetadataCodec(),
            TimeProvider.System);
        var activity = writer.Append(new TicketActivityAppendRequest(
            harness.Ticket.Id,
            activityType,
            TicketActivitySource.Portal,
            visibility,
            occurredAt,
            actorKind,
            actorUserId,
            externalRequesterId,
            displayName,
            actorRole,
            relatedStatusHistoryId,
            relatedMessageId,
            relatedAttachmentId,
            metadata));
        await harness.DbContext.SaveChangesAsync();
        return activity;
    }

    private static async Task<(int Activities, int StatusHistory, int Messages, int Attachments)> GetCanonicalCountsAsync(
        TicketServiceTests.TicketServiceHarness harness) =>
        (
            await harness.DbContext.TicketActivities.CountAsync(),
            await harness.DbContext.TicketStatusHistories.CountAsync(),
            await harness.DbContext.TicketMessages.CountAsync(),
            await harness.DbContext.Attachments.CountAsync());

    private static DateTime Utc(int hour, int minute) =>
        new(2026, 8, 25, hour, minute, 0, DateTimeKind.Utc);
}
