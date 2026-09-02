using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using SupportFlow.Api.Data;
using SupportFlow.Api.Enums;
using SupportFlow.Api.Models;
using SupportFlow.Api.Services;
using SupportFlow.Api.Services.TicketActivities;

namespace SupportFlow.Api.Tests.Attachments;

public sealed class AttachmentServiceTests
{
    [Fact]
    public async Task UploadAttachmentAsync_WithValidImage_PersistsSafely()
    {
        await using var harness = await AttachmentServiceHarness.CreateAsync();

        var result = await harness.Service.UploadAttachmentAsync(
            harness.CustomerTicket.Id,
            harness.Customer.Id,
            UserRole.Customer,
            CreateFormFile("ekran.png", "image/png", MinimalPngBytes),
            CancellationToken.None);

        var attachment = Assert.Single(await harness.DbContext.Attachments.ToListAsync());
        Assert.Equal(TicketOperationFailure.None, result.Failure);
        Assert.NotNull(result.Value);
        Assert.Equal("ekran.png", result.Value!.FileName);
        Assert.Equal("image/png", result.Value.ContentType);
        Assert.True(result.Value.IsImage);
        Assert.Equal(harness.Customer.Id, result.Value.UploadedByUserId);
        Assert.NotEqual("ekran.png", attachment.StoredFileName);
        Assert.DoesNotContain("ekran", attachment.StoredFileName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ekran", attachment.FilePath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("uploads/tickets/", attachment.FilePath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(harness.ContentRootPath, attachment.FilePath.Replace('/', Path.DirectorySeparatorChar))));

        var activity = Assert.Single(await harness.DbContext.TicketActivities.ToListAsync());
        Assert.Equal(TicketActivityType.AttachmentAdded, activity.ActivityType);
        Assert.Equal(TicketActivitySource.Portal, activity.Source);
        Assert.Equal(TicketActivityVisibility.RequesterVisible, activity.Visibility);
        Assert.Equal(attachment.Id, activity.RelatedAttachmentId);
        Assert.DoesNotContain(attachment.FilePath, activity.MetadataJson, StringComparison.Ordinal);
        Assert.DoesNotContain(attachment.StoredFileName, activity.MetadataJson, StringComparison.Ordinal);

        var serialized = JsonSerializer.Serialize(result.Value);
        Assert.DoesNotContain("FilePath", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("StoredFileName", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(harness.ContentRootPath, serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadAttachmentAsync_WithValidPdf_Succeeds()
    {
        await using var harness = await AttachmentServiceHarness.CreateAsync();

        var result = await harness.Service.UploadAttachmentAsync(
            harness.CustomerTicket.Id,
            harness.Customer.Id,
            UserRole.Customer,
            CreateFormFile("kanit.pdf", "application/pdf", MinimalPdfBytes),
            CancellationToken.None);

        Assert.Equal(TicketOperationFailure.None, result.Failure);
        Assert.NotNull(result.Value);
        Assert.False(result.Value!.IsImage);
        Assert.Equal("application/pdf", result.Value.ContentType);
    }

    [Fact]
    public async Task UploadAttachmentAsync_WithSignatureVerifiedPngAndGenericMime_Succeeds()
    {
        await using var harness = await AttachmentServiceHarness.CreateAsync();

        var result = await harness.Service.UploadAttachmentAsync(
            harness.CustomerTicket.Id,
            harness.Customer.Id,
            UserRole.Customer,
            CreateFormFile("kanit.png", "application/octet-stream", MinimalPngBytes),
            CancellationToken.None);

        Assert.Equal(TicketOperationFailure.None, result.Failure);
        Assert.NotNull(result.Value);
        Assert.Equal("image/png", result.Value!.ContentType);
    }

    [Theory]
    [InlineData("zararli.exe", "application/octet-stream")]
    [InlineData("ekran.png", "text/plain")]
    public async Task UploadAttachmentAsync_WithInvalidExtensionOrContentSignature_Fails(
        string fileName,
        string contentType)
    {
        await using var harness = await AttachmentServiceHarness.CreateAsync();

        var result = await harness.Service.UploadAttachmentAsync(
            harness.CustomerTicket.Id,
            harness.Customer.Id,
            UserRole.Customer,
            CreateFormFile(fileName, contentType, Encoding.UTF8.GetBytes("not-a-valid-file")),
            CancellationToken.None);

        Assert.Equal(TicketOperationFailure.InvalidRequest, result.Failure);
        Assert.Null(result.Value);
        Assert.Empty(await harness.DbContext.Attachments.ToListAsync());
    }

    [Fact]
    public async Task UploadAttachmentAsync_WithOversizedFile_Fails()
    {
        await using var harness = await AttachmentServiceHarness.CreateAsync();
        var oversizedBytes = new byte[AttachmentService.MaxFileSizeBytes + 1];
        Array.Copy(MinimalPngBytes, oversizedBytes, MinimalPngBytes.Length);

        var result = await harness.Service.UploadAttachmentAsync(
            harness.CustomerTicket.Id,
            harness.Customer.Id,
            UserRole.Customer,
            CreateFormFile("buyuk.png", "image/png", oversizedBytes),
            CancellationToken.None);

        Assert.Equal(TicketOperationFailure.InvalidRequest, result.Failure);
        Assert.Null(result.Value);
        Assert.Empty(await harness.DbContext.Attachments.ToListAsync());
    }

    [Fact]
    public async Task CustomerOwnTicketAccess_CanListAndDownloadAttachment()
    {
        await using var harness = await AttachmentServiceHarness.CreateAsync();
        var upload = await harness.UploadSeedAttachmentAsync();

        var listResult = await harness.Service.GetAttachmentsAsync(
            harness.CustomerTicket.Id,
            harness.Customer.Id,
            UserRole.Customer,
            CancellationToken.None);
        var downloadResult = await harness.Service.GetAttachmentFileAsync(
            harness.CustomerTicket.Id,
            upload.Id,
            harness.Customer.Id,
            UserRole.Customer,
            CancellationToken.None);

        Assert.Equal(TicketOperationFailure.None, listResult.Failure);
        Assert.Single(listResult.Value!);
        Assert.Equal(TicketOperationFailure.None, downloadResult.Failure);
        Assert.Equal("image/png", downloadResult.Value!.ContentType);
        await downloadResult.Value.Stream.DisposeAsync();
    }

    [Fact]
    public async Task CrossCustomerAttachmentAccess_ReturnsNotFound()
    {
        await using var harness = await AttachmentServiceHarness.CreateAsync();
        var upload = await harness.UploadSeedAttachmentAsync();

        var listResult = await harness.Service.GetAttachmentsAsync(
            harness.CustomerTicket.Id,
            harness.OtherCustomer.Id,
            UserRole.Customer,
            CancellationToken.None);
        var downloadResult = await harness.Service.GetAttachmentFileAsync(
            harness.CustomerTicket.Id,
            upload.Id,
            harness.OtherCustomer.Id,
            UserRole.Customer,
            CancellationToken.None);

        Assert.Equal(TicketOperationFailure.NotFound, listResult.Failure);
        Assert.Equal(TicketOperationFailure.NotFound, downloadResult.Failure);
    }

    [Fact]
    public async Task TechnicalStaffAccess_CanListAndDownloadAttachment()
    {
        await using var harness = await AttachmentServiceHarness.CreateAsync();
        var upload = await harness.UploadSeedAttachmentAsync();

        var listResult = await harness.Service.GetAttachmentsAsync(
            harness.CustomerTicket.Id,
            harness.TechnicalStaff.Id,
            UserRole.TechnicalStaff,
            CancellationToken.None);
        var downloadResult = await harness.Service.GetAttachmentFileAsync(
            harness.CustomerTicket.Id,
            upload.Id,
            harness.TechnicalStaff.Id,
            UserRole.TechnicalStaff,
            CancellationToken.None);

        Assert.Equal(TicketOperationFailure.None, listResult.Failure);
        Assert.Single(listResult.Value!);
        Assert.Equal(TicketOperationFailure.None, downloadResult.Failure);
        await downloadResult.Value!.Stream.DisposeAsync();
    }

    private static IFormFile CreateFormFile(string fileName, string contentType, byte[] bytes)
    {
        var stream = new MemoryStream(bytes);

        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static readonly byte[] MinimalPngBytes =
        Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aF9sAAAAASUVORK5CYII=");

    private static readonly byte[] MinimalPdfBytes =
        Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<<>>\nendobj\ntrailer\n<<>>\n%%EOF");

    private sealed class AttachmentServiceHarness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private AttachmentServiceHarness(
            SqliteConnection connection,
            AppDbContext dbContext,
            AttachmentService service,
            string contentRootPath,
            User customer,
            User otherCustomer,
            User technicalStaff,
            Ticket customerTicket)
        {
            _connection = connection;
            DbContext = dbContext;
            Service = service;
            ContentRootPath = contentRootPath;
            Customer = customer;
            OtherCustomer = otherCustomer;
            TechnicalStaff = technicalStaff;
            CustomerTicket = customerTicket;
        }

        public AppDbContext DbContext { get; }
        public AttachmentService Service { get; }
        public string ContentRootPath { get; }
        public User Customer { get; }
        public User OtherCustomer { get; }
        public User TechnicalStaff { get; }
        public Ticket CustomerTicket { get; }

        public static async Task<AttachmentServiceHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var dbContext = new AppDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            var customer = new User
            {
                FirstName = "Takashi",
                LastName = "Hyugo",
                Email = "customer@example.test",
                PasswordHash = "hash",
                Role = UserRole.Customer,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var otherCustomer = new User
            {
                FirstName = "Aylin",
                LastName = "Demir",
                Email = "other@example.test",
                PasswordHash = "hash",
                Role = UserRole.Customer,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var technicalStaff = new User
            {
                FirstName = "Tech",
                LastName = "User",
                Email = "tech@example.test",
                PasswordHash = "hash",
                Role = UserRole.TechnicalStaff,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Users.AddRange(customer, otherCustomer, technicalStaff);
            await dbContext.SaveChangesAsync();

            var ticket = new Ticket
            {
                TicketNumber = "SUP-2026-001010",
                CustomerId = customer.Id,
                Title = "Baglanti sorunu",
                Description = "desc",
                Status = TicketStatus.New,
                Priority = TicketPriority.Normal,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
            };
            dbContext.Tickets.Add(ticket);
            await dbContext.SaveChangesAsync();

            var contentRootPath = Path.Combine(
                Path.GetTempPath(),
                "supportflow-attachment-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(contentRootPath);

            var service = new AttachmentService(
                dbContext,
                new TestHostEnvironment(contentRootPath),
                NullLogger<AttachmentService>.Instance,
                new TicketActivityWriter(dbContext, new TicketActivityMetadataCodec(), TimeProvider.System));

            return new AttachmentServiceHarness(
                connection,
                dbContext,
                service,
                contentRootPath,
                customer,
                otherCustomer,
                technicalStaff,
                ticket);
        }

        public async Task<Attachment> UploadSeedAttachmentAsync()
        {
            var uploadResult = await Service.UploadAttachmentAsync(
                CustomerTicket.Id,
                Customer.Id,
                UserRole.Customer,
                CreateFormFile("seed.png", "image/png", MinimalPngBytes),
                CancellationToken.None);

            Assert.Equal(TicketOperationFailure.None, uploadResult.Failure);
            return Assert.Single(await DbContext.Attachments.ToListAsync());
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();

            if (Directory.Exists(ContentRootPath))
            {
                Directory.Delete(ContentRootPath, recursive: true);
            }
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
            ApplicationName = "SupportFlow.Api.Tests";
            EnvironmentName = Environments.Development;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; }
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
    }
}
