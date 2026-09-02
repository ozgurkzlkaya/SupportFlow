using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using SupportFlow.Api.Data;
using SupportFlow.Api.DTOs.Attachments;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services.TicketActivities;

namespace SupportFlow.Api.Services;

public class AttachmentService
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;
    public const int MaxAttachmentsPerTicket = 5;

    private static readonly IReadOnlyDictionary<string, string> AllowedContentTypesByExtension =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".webp"] = "image/webp",
            [".pdf"] = "application/pdf"
        };

    private readonly AppDbContext _dbContext;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AttachmentService> _logger;
    private readonly ITicketActivityWriter _ticketActivityWriter;

    public AttachmentService(
        AppDbContext dbContext,
        IHostEnvironment environment,
        ILogger<AttachmentService> logger,
        ITicketActivityWriter ticketActivityWriter)
    {
        _dbContext = dbContext;
        _environment = environment;
        _logger = logger;
        _ticketActivityWriter = ticketActivityWriter;
    }

    public async Task<TicketOperationResult<IReadOnlyList<TicketAttachmentResponse>>> GetAttachmentsAsync(
        int ticketId,
        int currentUserId,
        UserRole currentUserRole,
        CancellationToken cancellationToken = default)
    {
        if (!await CanAccessTicketAsync(ticketId, currentUserId, currentUserRole, cancellationToken))
        {
            return TicketOperationResult<IReadOnlyList<TicketAttachmentResponse>>.Failed(
                TicketOperationFailure.NotFound);
        }

        var attachments = await _dbContext.Attachments
            .AsNoTracking()
            .Where(attachment => attachment.TicketId == ticketId && attachment.MessageId == null)
            .OrderBy(attachment => attachment.CreatedAt)
            .Select(attachment => new TicketAttachmentResponse
            {
                Id = attachment.Id,
                TicketId = attachment.TicketId,
                MessageId = attachment.MessageId,
                FileName = attachment.OriginalFileName,
                ContentType = attachment.ContentType,
                FileSize = attachment.FileSize,
                IsImage = IsImageContentType(attachment.ContentType),
                CreatedAt = attachment.CreatedAt,
                UploadedByUserId = attachment.UploadedByUserId,
                UploadedByUserName = attachment.UploadedByUser == null ? string.Empty : attachment.UploadedByUser.FirstName + " " + attachment.UploadedByUser.LastName,
                UploadedByExternalRequesterId = attachment.UploadedByExternalRequesterId,
                UploadedByExternalRequesterName = attachment.UploadedByExternalRequester == null
                    ? string.Empty
                    : attachment.UploadedByExternalRequester.DisplayName ?? attachment.UploadedByExternalRequester.Email
            })
            .ToListAsync(cancellationToken);

        return TicketOperationResult<IReadOnlyList<TicketAttachmentResponse>>.Succeeded(attachments);
    }

    public async Task<TicketOperationResult<TicketAttachmentResponse>> UploadAttachmentAsync(
        int ticketId,
        int currentUserId,
        UserRole currentUserRole,
        IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (!await CanAccessTicketAsync(ticketId, currentUserId, currentUserRole, cancellationToken))
        {
            return TicketOperationResult<TicketAttachmentResponse>.Failed(
                TicketOperationFailure.NotFound);
        }

        if (file is null || file.Length <= 0)
        {
            return TicketOperationResult<TicketAttachmentResponse>.Failed(
                TicketOperationFailure.InvalidRequest,
                "A valid attachment file is required.");
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);

        return await PersistAttachmentAsync(
            ticketId,
            null,
            currentUserId,
            null,
            file.FileName,
            buffer.ToArray(),
            DateTime.UtcNow,
            TicketActivitySource.Portal,
            cancellationToken);
    }

    internal Task<TicketOperationResult<TicketAttachmentResponse>> SaveInboundAttachmentAsync(
        int ticketId,
        int uploadedByUserId,
        string fileName,
        byte[] contentBytes,
        DateTime createdAt,
        CancellationToken cancellationToken = default) =>
        PersistAttachmentAsync(
            ticketId,
            null,
            uploadedByUserId,
            null,
            fileName,
            contentBytes,
            createdAt,
            TicketActivitySource.GmailInbound,
            cancellationToken);

    internal Task<TicketOperationResult<TicketAttachmentResponse>> SaveInboundExternalRequesterAttachmentAsync(
        int ticketId,
        int? messageId,
        int uploadedByExternalRequesterId,
        string fileName,
        byte[] contentBytes,
        DateTime createdAt,
        CancellationToken cancellationToken = default) =>
        PersistAttachmentAsync(
            ticketId,
            messageId,
            null,
            uploadedByExternalRequesterId,
            fileName,
            contentBytes,
            createdAt,
            TicketActivitySource.GmailInbound,
            cancellationToken);

    public async Task<TicketOperationResult<TicketAttachmentFileResult>> GetAttachmentFileAsync(
        int ticketId,
        int attachmentId,
        int currentUserId,
        UserRole currentUserRole,
        CancellationToken cancellationToken = default)
    {
        if (!await CanAccessTicketAsync(ticketId, currentUserId, currentUserRole, cancellationToken))
        {
            return TicketOperationResult<TicketAttachmentFileResult>.Failed(
                TicketOperationFailure.NotFound);
        }

        var attachment = await _dbContext.Attachments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == attachmentId && item.TicketId == ticketId,
                cancellationToken);

        if (attachment is null)
        {
            return TicketOperationResult<TicketAttachmentFileResult>.Failed(
                TicketOperationFailure.NotFound);
        }

        var absoluteFilePath = Path.GetFullPath(
            Path.Combine(_environment.ContentRootPath, attachment.FilePath));
        var uploadsRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "uploads"));

        if (!absoluteFilePath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(absoluteFilePath))
        {
            return TicketOperationResult<TicketAttachmentFileResult>.Failed(
                TicketOperationFailure.NotFound);
        }

        var stream = new FileStream(
            absoluteFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        return TicketOperationResult<TicketAttachmentFileResult>.Succeeded(
            new TicketAttachmentFileResult(
                stream,
                attachment.ContentType,
                attachment.OriginalFileName));
    }

    private async Task<bool> CanAccessTicketAsync(
        int ticketId,
        int currentUserId,
        UserRole currentUserRole,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.Id == ticketId);

        if (currentUserRole == UserRole.Customer)
        {
            query = query.Where(ticket => ticket.CustomerId == currentUserId);
        }

        return await query.AnyAsync(cancellationToken);
    }

    private async Task<TicketOperationResult<TicketAttachmentResponse>> PersistAttachmentAsync(
        int ticketId,
        int? messageId,
        int? uploadedByUserId,
        int? uploadedByExternalRequesterId,
        string originalFileName,
        byte[] contentBytes,
        DateTime createdAt,
        TicketActivitySource activitySource,
        CancellationToken cancellationToken)
    {
        if (uploadedByUserId.HasValue == uploadedByExternalRequesterId.HasValue)
        {
            throw new ArgumentException("An attachment must have exactly one uploader.");
        }

        if (contentBytes.Length == 0)
        {
            return TicketOperationResult<TicketAttachmentResponse>.Failed(
                TicketOperationFailure.InvalidRequest,
                "A valid attachment file is required.");
        }

        if (contentBytes.LongLength > MaxFileSizeBytes)
        {
            return TicketOperationResult<TicketAttachmentResponse>.Failed(
                TicketOperationFailure.InvalidRequest,
                "Each attachment must be 5 MB or smaller.");
        }

        var existingAttachmentCount = await _dbContext.Attachments
            .AsNoTracking()
            .CountAsync(
                attachment => attachment.TicketId == ticketId,
                cancellationToken);

        if (existingAttachmentCount >= MaxAttachmentsPerTicket)
        {
            return TicketOperationResult<TicketAttachmentResponse>.Failed(
                TicketOperationFailure.InvalidRequest,
                "A ticket can have at most 5 attachments in this phase.");
        }

        var sanitizedOriginalName = Path.GetFileName(originalFileName).Trim();
        var extension = Path.GetExtension(sanitizedOriginalName).ToLowerInvariant();

        if (!AllowedContentTypesByExtension.TryGetValue(extension, out var expectedContentType))
        {
            return TicketOperationResult<TicketAttachmentResponse>.Failed(
                TicketOperationFailure.InvalidRequest,
                "Only PNG, JPG, WEBP and PDF files are allowed.");
        }

        var detectedContentType = DetectContentType(contentBytes);
        if (!string.Equals(detectedContentType, expectedContentType, StringComparison.OrdinalIgnoreCase))
        {
            return TicketOperationResult<TicketAttachmentResponse>.Failed(
                TicketOperationFailure.InvalidRequest,
                "The uploaded file content does not match an allowed attachment type.");
        }

        var ticketDirectory = Path.Combine(
            _environment.ContentRootPath,
            "uploads",
            "tickets",
            ticketId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(ticketDirectory);

        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var absoluteFilePath = Path.GetFullPath(Path.Combine(ticketDirectory, storedFileName));
        var uploadsRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "uploads"));

        if (!absoluteFilePath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Attachment upload path escaped the configured uploads root for ticket {TicketId}.",
                ticketId);
            return TicketOperationResult<TicketAttachmentResponse>.Failed(
                TicketOperationFailure.InvalidRequest,
                "The attachment path is invalid.");
        }

        var relativeFilePath = Path.GetRelativePath(_environment.ContentRootPath, absoluteFilePath)
            .Replace('\\', '/');
        var safeOriginalName = string.IsNullOrWhiteSpace(sanitizedOriginalName)
            ? $"attachment{extension}"
            : sanitizedOriginalName;
        var uploader = uploadedByUserId.HasValue
            ? await _dbContext.Users
                .AsNoTracking()
                .Where(user => user.Id == uploadedByUserId.Value)
                .Select(user => new AttachmentUploader(user.Id, user.FirstName + " " + user.LastName, user.Role, null, string.Empty))
                .SingleAsync(cancellationToken)
            : await _dbContext.ExternalRequesters
                .AsNoTracking()
                .Where(requester => requester.Id == uploadedByExternalRequesterId!.Value)
                .Select(requester => new AttachmentUploader(null, string.Empty, null, requester.Id, requester.DisplayName ?? requester.Email))
                .SingleAsync(cancellationToken);

        try
        {
            await using (var fileStream = new FileStream(
                             absoluteFilePath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None))
            {
                await fileStream.WriteAsync(contentBytes, cancellationToken);
            }

            var attachment = new Attachment
            {
                TicketId = ticketId,
                MessageId = messageId,
                UploadedByUserId = uploadedByUserId,
                UploadedByExternalRequesterId = uploadedByExternalRequesterId,
                OriginalFileName = safeOriginalName,
                StoredFileName = storedFileName,
                FilePath = relativeFilePath,
                ContentType = expectedContentType,
                FileSize = contentBytes.LongLength,
                CreatedAt = EnsureUtc(createdAt)
            };

            var ownsTransaction = _dbContext.Database.CurrentTransaction is null;
            await using var transaction = ownsTransaction
                ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
                : null;

            _dbContext.Attachments.Add(attachment);
            await _dbContext.SaveChangesAsync(cancellationToken);
            AppendAttachmentActivity(attachment, uploader, activitySource);
            await _dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return TicketOperationResult<TicketAttachmentResponse>.Succeeded(new TicketAttachmentResponse
            {
                Id = attachment.Id,
                TicketId = attachment.TicketId,
                MessageId = attachment.MessageId,
                FileName = attachment.OriginalFileName,
                ContentType = attachment.ContentType,
                FileSize = attachment.FileSize,
                IsImage = IsImageContentType(attachment.ContentType),
                CreatedAt = attachment.CreatedAt,
                UploadedByUserId = uploader.UserId,
                UploadedByUserName = uploader.UserName,
                UploadedByExternalRequesterId = uploader.ExternalRequesterId,
                UploadedByExternalRequesterName = uploader.ExternalRequesterName
            });
        }
        catch
        {
            TryDeleteFile(absoluteFilePath);
            throw;
        }
    }

    private sealed record AttachmentUploader(
        int? UserId,
        string UserName,
        UserRole? UserRole,
        int? ExternalRequesterId,
        string ExternalRequesterName);

    private void AppendAttachmentActivity(
        Attachment attachment,
        AttachmentUploader uploader,
        TicketActivitySource source)
    {
        if (uploader.UserId.HasValue)
        {
            _ticketActivityWriter.Append(new TicketActivityAppendRequest(
                attachment.TicketId,
                TicketActivityType.AttachmentAdded,
                source,
                TicketActivityVisibility.RequesterVisible,
                attachment.CreatedAt,
                TicketActivityActorKind.User,
                uploader.UserId.Value,
                null,
                uploader.UserName.Trim(),
                uploader.UserRole,
                RelatedAttachmentId: attachment.Id));
            return;
        }

        _ticketActivityWriter.Append(new TicketActivityAppendRequest(
            attachment.TicketId,
            TicketActivityType.AttachmentAdded,
            source,
            TicketActivityVisibility.RequesterVisible,
            attachment.CreatedAt,
            TicketActivityActorKind.ExternalRequester,
            null,
            uploader.ExternalRequesterId,
            uploader.ExternalRequesterName.Trim(),
            null,
            RelatedAttachmentId: attachment.Id));
    }

    private static string? DetectContentType(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 8
            && header[0] == 0x89
            && header[1] == 0x50
            && header[2] == 0x4E
            && header[3] == 0x47
            && header[4] == 0x0D
            && header[5] == 0x0A
            && header[6] == 0x1A
            && header[7] == 0x0A)
        {
            return "image/png";
        }

        if (header.Length >= 3
            && header[0] == 0xFF
            && header[1] == 0xD8
            && header[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (header.Length >= 12
            && header[0] == 0x52
            && header[1] == 0x49
            && header[2] == 0x46
            && header[3] == 0x46
            && header[8] == 0x57
            && header[9] == 0x45
            && header[10] == 0x42
            && header[11] == 0x50)
        {
            return "image/webp";
        }

        if (header.Length >= 5
            && header[0] == 0x25
            && header[1] == 0x50
            && header[2] == 0x44
            && header[3] == 0x46
            && header[4] == 0x2D)
        {
            return "application/pdf";
        }

        return null;
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static bool IsImageContentType(string contentType) =>
        contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    private static void TryDeleteFile(string absoluteFilePath)
    {
        try
        {
            if (File.Exists(absoluteFilePath))
            {
                File.Delete(absoluteFilePath);
            }
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }
}

public sealed record TicketAttachmentFileResult(
    Stream Stream,
    string ContentType,
    string FileName);
