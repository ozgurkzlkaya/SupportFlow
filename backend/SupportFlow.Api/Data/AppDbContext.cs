using Microsoft.EntityFrameworkCore;
using SupportFlow.Api.DTOs.Conversations;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;

namespace SupportFlow.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ExternalRequester> ExternalRequesters => Set<ExternalRequester>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<TicketStatusHistory> TicketStatusHistories => Set<TicketStatusHistory>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<TicketEmailThread> TicketEmailThreads => Set<TicketEmailThread>();
    public DbSet<ProcessedEmailMessage> ProcessedEmailMessages => Set<ProcessedEmailMessage>();
    public DbSet<ConversationPin> ConversationPins => Set<ConversationPin>();
    public DbSet<ConversationReadState> ConversationReadStates => Set<ConversationReadState>();
    public DbSet<UserAdminAudit> UserAdminAudits => Set<UserAdminAudit>();
    public DbSet<TicketCategory> TicketCategories => Set<TicketCategory>();
    public DbSet<TicketTag> TicketTags => Set<TicketTag>();
    public DbSet<TicketTagAssignment> TicketTagAssignments => Set<TicketTagAssignment>();
    public DbSet<TicketActivity> TicketActivities => Set<TicketActivity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Company>(entity =>
        {
            entity.Property(company => company.Name)
                .HasMaxLength(200);

            entity.HasMany(company => company.Users)
                .WithOne(user => user.Company)
                .HasForeignKey(user => user.CompanyId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(user => user.Email)
                .IsUnique();

            entity.Property(user => user.FirstName)
                .HasMaxLength(100);

            entity.Property(user => user.LastName)
                .HasMaxLength(100);

            entity.Property(user => user.Email)
                .HasMaxLength(255);

            entity.Property(user => user.Role)
                .HasConversion<string>()
                .HasMaxLength(50);
        });

        modelBuilder.Entity<ExternalRequester>(entity =>
        {
            entity.Property(requester => requester.Email).HasMaxLength(255).IsRequired();
            entity.Property(requester => requester.NormalizedEmail).HasMaxLength(255).IsRequired();
            entity.Property(requester => requester.DisplayName).HasMaxLength(200);
            entity.HasIndex(requester => requester.NormalizedEmail).IsUnique();
        });

        modelBuilder.Entity<ConversationPin>(entity =>
        {
            entity.Property(pin => pin.RequesterType)
                .HasConversion<string>()
                .HasMaxLength(30);

            entity.HasIndex(pin => new { pin.UserId, pin.RequesterType, pin.RequesterId })
                .IsUnique();

            entity.HasIndex(pin => new { pin.UserId, pin.CreatedAt });

            entity.HasOne(pin => pin.User)
                .WithMany(user => user.ConversationPins)
                .HasForeignKey(pin => pin.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ConversationReadState>(entity =>
        {
            entity.Property(state => state.RequesterType)
                .HasConversion<string>()
                .HasMaxLength(30);

            entity.HasIndex(state => new { state.UserId, state.RequesterType, state.RequesterId })
                .IsUnique();

            entity.HasIndex(state => new { state.UserId, state.LastReadAt });

            entity.HasOne(state => state.User)
                .WithMany(user => user.ConversationReadStates)
                .HasForeignKey(state => state.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<UserAdminAudit>(entity =>
        {
            entity.Property(audit => audit.ActorDisplayName)
                .HasMaxLength(201)
                .IsRequired();

            entity.Property(audit => audit.TargetDisplayName)
                .HasMaxLength(201)
                .IsRequired();

            entity.Property(audit => audit.TargetEmail)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(audit => audit.TargetRole)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(audit => audit.Action)
                .HasConversion<string>()
                .HasMaxLength(30);

            entity.Property(audit => audit.OccurredAt)
                .HasColumnType("datetime2");

            entity.HasIndex(audit => new { audit.TargetUserId, audit.OccurredAt });
            entity.HasIndex(audit => audit.ActorUserId);

            entity.HasOne(audit => audit.ActorUser)
                .WithMany(user => user.PerformedUserAdminAudits)
                .HasForeignKey(audit => audit.ActorUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(audit => audit.TargetUser)
                .WithMany(user => user.TargetedUserAdminAudits)
                .HasForeignKey(audit => audit.TargetUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasIndex(ticket => ticket.TicketNumber)
                .IsUnique();

            entity.Property(ticket => ticket.TicketNumber)
                .HasMaxLength(30);

            entity.Property(ticket => ticket.Title)
                .HasMaxLength(250);

            entity.Property(ticket => ticket.Status)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(ticket => ticket.Priority)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(ticket => ticket.FirstResponseDueAt)
                .HasColumnType("datetime2");

            entity.Property(ticket => ticket.FirstRespondedAt)
                .HasColumnType("datetime2");

            entity.Property(ticket => ticket.ResolutionCycleStartedAt)
                .HasColumnType("datetime2");

            entity.Property(ticket => ticket.ResolutionDueAt)
                .HasColumnType("datetime2");

            entity.Property(ticket => ticket.ResolvedAt)
                .HasColumnType("datetime2");

            entity.HasIndex(ticket => ticket.FirstResponseDueAt);
            entity.HasIndex(ticket => ticket.ResolutionDueAt);
            entity.HasIndex(ticket => ticket.CategoryId);

            entity.HasOne(ticket => ticket.Customer)
                .WithMany(user => user.CreatedTickets)
                .HasForeignKey(ticket => ticket.CustomerId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(ticket => ticket.ExternalRequester)
                .WithMany(requester => requester.Tickets)
                .HasForeignKey(ticket => ticket.ExternalRequesterId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(ticket => ticket.AssignedUser)
                .WithMany(user => user.AssignedTickets)
                .HasForeignKey(ticket => ticket.AssignedUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(ticket => ticket.Category)
                .WithMany(category => category.Tickets)
                .HasForeignKey(ticket => ticket.CategoryId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<TicketCategory>(entity =>
        {
            entity.Property(category => category.Name).HasMaxLength(150).IsRequired();
            entity.Property(category => category.NormalizedName).HasMaxLength(150).IsRequired();
            entity.Property(category => category.Description).HasMaxLength(500);
            entity.HasIndex(category => category.NormalizedName).IsUnique();
        });

        modelBuilder.Entity<TicketTag>(entity =>
        {
            entity.Property(tag => tag.Name).HasMaxLength(100).IsRequired();
            entity.Property(tag => tag.NormalizedName).HasMaxLength(100).IsRequired();
            entity.HasIndex(tag => tag.NormalizedName).IsUnique();
        });

        modelBuilder.Entity<TicketTagAssignment>(entity =>
        {
            entity.HasKey(assignment => new { assignment.TicketId, assignment.TagId });
            entity.HasIndex(assignment => assignment.TagId);

            entity.HasOne(assignment => assignment.Ticket)
                .WithMany(ticket => ticket.TagAssignments)
                .HasForeignKey(assignment => assignment.TicketId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(assignment => assignment.Tag)
                .WithMany(tag => tag.Assignments)
                .HasForeignKey(assignment => assignment.TagId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<TicketMessage>(entity =>
        {
            entity.Property(message => message.OutboundDeliveryStatus)
                .HasConversion<string>()
                .HasMaxLength(30)
                .HasDefaultValue(OutboundDeliveryStatus.NotApplicable);

            entity.Property(message => message.OutboundDeliveryError)
                .HasMaxLength(500);

            entity.HasOne(message => message.Ticket)
                .WithMany(ticket => ticket.Messages)
                .HasForeignKey(message => message.TicketId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(message => message.ExternalRequester)
                .WithMany(requester => requester.TicketMessages)
                .HasForeignKey(message => message.ExternalRequesterId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(message => message.Sender)
                .WithMany(user => user.Messages)
                .HasForeignKey(message => message.SenderId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<TicketStatusHistory>(entity =>
        {
            entity.Property(history => history.OldStatus)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(history => history.NewStatus)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.HasOne(history => history.Ticket)
                .WithMany(ticket => ticket.StatusHistory)
                .HasForeignKey(history => history.TicketId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(history => history.ChangedByUser)
                .WithMany()
                .HasForeignKey(history => history.ChangedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<TicketActivity>(entity =>
        {
            entity.Property(activity => activity.ActivityType)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(activity => activity.Source)
                .HasConversion<string>()
                .HasMaxLength(30);

            entity.Property(activity => activity.Visibility)
                .HasConversion<string>()
                .HasMaxLength(30);

            entity.Property(activity => activity.ActorKind)
                .HasConversion<string>()
                .HasMaxLength(30);

            entity.Property(activity => activity.ActorRole)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(activity => activity.ActorDisplayName)
                .HasMaxLength(201)
                .IsRequired();

            entity.Property(activity => activity.OccurredAt).HasColumnType("datetime2");
            entity.Property(activity => activity.RecordedAt).HasColumnType("datetime2");
            entity.Property(activity => activity.MetadataJson).HasMaxLength(4000).IsRequired();

            entity.HasIndex(activity => new { activity.TicketId, activity.OccurredAt, activity.Id })
                .IsDescending(false, true, true);
            entity.HasIndex(activity => new { activity.ActorUserId, activity.OccurredAt })
                .IsDescending(false, true);
            entity.HasIndex(activity => new { activity.ExternalRequesterId, activity.OccurredAt })
                .IsDescending(false, true);
            entity.HasIndex(activity => new { activity.ActivityType, activity.OccurredAt })
                .IsDescending(false, true);

            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_TicketActivities_ActorKind",
                    "([ActorKind] = 'User' AND [ActorUserId] IS NOT NULL AND [ExternalRequesterId] IS NULL) OR ([ActorKind] = 'ExternalRequester' AND [ActorUserId] IS NULL AND [ExternalRequesterId] IS NOT NULL) OR ([ActorKind] = 'System' AND [ActorUserId] IS NULL AND [ExternalRequesterId] IS NULL)");
                table.HasCheckConstraint("CK_TicketActivities_MetadataVersion", "[MetadataVersion] > 0");
            });

            entity.HasOne(activity => activity.Ticket)
                .WithMany(ticket => ticket.Activities)
                .HasForeignKey(activity => activity.TicketId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(activity => activity.ActorUser)
                .WithMany()
                .HasForeignKey(activity => activity.ActorUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(activity => activity.ExternalRequester)
                .WithMany()
                .HasForeignKey(activity => activity.ExternalRequesterId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(activity => activity.RelatedStatusHistory)
                .WithMany()
                .HasForeignKey(activity => activity.RelatedStatusHistoryId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(activity => activity.RelatedMessage)
                .WithMany()
                .HasForeignKey(activity => activity.RelatedMessageId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(activity => activity.RelatedAttachment)
                .WithMany()
                .HasForeignKey(activity => activity.RelatedAttachmentId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Attachment>(entity =>
        {
            entity.HasOne(attachment => attachment.Ticket)
                .WithMany()
                .HasForeignKey(attachment => attachment.TicketId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(attachment => attachment.Message)
                .WithMany()
                .HasForeignKey(attachment => attachment.MessageId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(attachment => attachment.UploadedByUser)
                .WithMany()
                .HasForeignKey(attachment => attachment.UploadedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(attachment => attachment.UploadedByExternalRequester)
                .WithMany(requester => requester.Attachments)
                .HasForeignKey(attachment => attachment.UploadedByExternalRequesterId)
                .OnDelete(DeleteBehavior.NoAction);


        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.Property(notification => notification.Type)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.HasOne(notification => notification.User)
                .WithMany()
                .HasForeignKey(notification => notification.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(notification => notification.Ticket)
                .WithMany()
                .HasForeignKey(notification => notification.TicketId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<TicketEmailThread>(entity =>
        {
            entity.HasKey(thread => thread.Id);

            entity.Property(thread => thread.GmailThreadId)
                .HasMaxLength(255)
                .IsRequired();

            entity.HasIndex(thread => thread.GmailThreadId)
                .IsUnique();

            entity.HasIndex(thread => thread.TicketId)
                .IsUnique(false);

            entity.HasOne(thread => thread.Ticket)
                .WithMany(ticket => ticket.EmailThreads)
                .HasForeignKey(thread => thread.TicketId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ProcessedEmailMessage>(entity =>
        {
            entity.HasKey(message => message.Id);

            entity.Property(message => message.GmailMessageId)
                .HasMaxLength(255)
                .IsRequired();

            entity.HasIndex(message => message.GmailMessageId)
                .IsUnique();

            entity.Property(message => message.InternetMessageId)
                .HasMaxLength(998);

            entity.HasIndex(message => message.InternetMessageId);

            entity.HasIndex(message => message.TicketEmailThreadId);

            entity.HasOne(message => message.TicketEmailThread)
                .WithMany(thread => thread.ProcessedMessages)
                .HasForeignKey(message => message.TicketEmailThreadId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(message => message.TicketMessage)
                .WithMany()
                .HasForeignKey(message => message.TicketMessageId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
