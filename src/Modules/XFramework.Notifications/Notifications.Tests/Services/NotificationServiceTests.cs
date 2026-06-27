using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Notifications.Api.Services;
using Notifications.Domain.Shared.Contracts;
using Notifications.Domain.Shared.Contracts.Requests;
using Notifications.Domain.Shared.Enums;
using SmsGateway.Domain.Shared.Contracts.Requests.Create;
using SmsGateway.Domain.Shared.Contracts.Requests.Get;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;
using SmsGateway.Integration.Drivers;
using NUnit.Framework;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.BusinessObjects;

namespace Notifications.Tests.Services;

public sealed class NotificationServiceTests
{
    [Test]
    public async Task GetPreferencesAsync_MissingPreference_ReturnsDefaultInAppPreference()
    {
        await using var database = await TestDatabase.CreateAsync();
        var credentialId = Guid.NewGuid();
        var service = CreateService(database.Context);

        var result = await service.GetPreferencesAsync(database.TenantId, credentialId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.CredentialId.Should().Be(credentialId);
        result.Data.EnabledChannels.Should().Be(NotificationDeliveryChannel.InApp);
        result.Data.DisabledTemplateKeys.Should().BeEmpty();
        result.Data.IsDefault.Should().BeTrue();
    }

    [Test]
    public async Task GetInboxAsync_DifferentTenantRows_ReturnsOnlyRequestedTenantNotifications()
    {
        await using var database = await TestDatabase.CreateAsync();
        var credentialId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var service = CreateService(database.Context);

        database.Context.Set<NotificationInboxItem>().AddRange(
            CreateInboxItem(database.TenantId, credentialId, "Current tenant"),
            CreateInboxItem(otherTenantId, credentialId, "Other tenant"));
        await database.Context.SaveChangesAsync();

        var result = await service.GetInboxAsync(new GetNotificationInboxRequest
        {
            TenantId = database.TenantId,
            RecipientCredentialId = credentialId
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.TotalCount.Should().Be(1);
        result.Data.Items.Should().ContainSingle();
        result.Data.Items[0].Title.Should().Be("Current tenant");
        result.Data.Items[0].TenantId.Should().Be(database.TenantId);
    }

    [Test]
    public async Task CreateNotificationAsync_SameCorrelationId_ReturnsExistingNotification()
    {
        await using var database = await TestDatabase.CreateAsync();
        var credentialId = Guid.NewGuid();
        var correlationId = $"communications:{Guid.NewGuid():N}";
        var service = CreateService(database.Context);

        var request = new CreateNotificationRequest
        {
            TenantId = database.TenantId,
            RecipientCredentialId = credentialId,
            TemplateKey = NotificationTemplateKeys.SystemGeneric,
            Title = "First title",
            Body = "First body",
            DeliveryChannels = NotificationDeliveryChannel.InApp,
            CorrelationId = correlationId
        };

        var first = await service.CreateNotificationAsync(request, CancellationToken.None);
        request.Title = "Updated title";
        var second = await service.CreateNotificationAsync(request, CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        second.Data!.Id.Should().Be(first.Data!.Id);
        second.Data.Title.Should().Be("First title");

        var rowCount = await database.Context.Set<NotificationInboxItem>()
            .CountAsync(item => item.TenantId == database.TenantId && item.CorrelationId == correlationId);
        rowCount.Should().Be(1);
    }

    [Test]
    public async Task CreateNotificationAsync_ExternalChannel_CreatesDeliveryJobAndStatus()
    {
        await using var database = await TestDatabase.CreateAsync();
        var credentialId = Guid.NewGuid();
        EnableChannels(database.Context, database.TenantId, credentialId, NotificationDeliveryChannel.Email);
        var service = CreateService(database.Context);

        var result = await service.CreateNotificationAsync(new CreateNotificationRequest
        {
            TenantId = database.TenantId,
            RecipientCredentialId = credentialId,
            TemplateKey = NotificationTemplateKeys.SystemGeneric,
            Title = "Email title",
            Body = "Email body",
            DeliveryChannels = NotificationDeliveryChannel.Email,
            DeliveryAddress = "person@example.test",
            CorrelationId = "email-test"
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var job = await database.Context.Set<NotificationDeliveryJob>().SingleAsync();
        job.Channel.Should().Be(NotificationDeliveryChannel.Email);
        job.Status.Should().Be(NotificationDeliveryStatus.Queued);
        job.RecipientAddress.Should().Be("person@example.test");
        job.CorrelationId.Should().Be("email-test:Email");

        var status = await database.Context.Set<NotificationDeliveryStatusRecord>().SingleAsync();
        status.Status.Should().Be(NotificationDeliveryStatus.Queued);
        status.Channel.Should().Be(NotificationDeliveryChannel.Email);
    }

    [Test]
    public async Task DispatchDueAsync_SmsJob_EnqueuesSmsGatewayMessage()
    {
        await using var database = await TestDatabase.CreateAsync();
        var credentialId = Guid.NewGuid();
        EnableChannels(database.Context, database.TenantId, credentialId, NotificationDeliveryChannel.Sms);
        var smsGateway = new TestSmsGatewayServiceWrapper();
        var agentClusterId = Guid.NewGuid();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:Delivery:Sms:AgentClusterId"] = agentClusterId.ToString()
            })
            .Build();

        var service = CreateService(database.Context);
        var create = await service.CreateNotificationAsync(new CreateNotificationRequest
        {
            TenantId = database.TenantId,
            RecipientCredentialId = credentialId,
            TemplateKey = NotificationTemplateKeys.SystemGeneric,
            Title = "SMS title",
            Body = "SMS body",
            DeliveryChannels = NotificationDeliveryChannel.Sms,
            DeliveryAddress = "+15555550123",
            CorrelationId = "sms-test"
        }, CancellationToken.None);

        create.IsSuccess.Should().BeTrue();

        var dispatcher = new NotificationDeliveryDispatcher(
            database.Context,
            smsGateway,
            NullLogger<NotificationDeliveryDispatcher>.Instance,
            configuration);

        var processed = await dispatcher.DispatchDueAsync(CancellationToken.None);

        processed.Should().Be(1);
        smsGateway.CreatedRequests.Should().ContainSingle();
        smsGateway.CreatedRequests[0].AgentClusterId.Should().Be(agentClusterId);
        smsGateway.CreatedRequests[0].Recipient.Should().Be("+15555550123");
        smsGateway.CreatedRequests[0].CorrelationId.Should().Be("sms-test:Sms");
        smsGateway.CreatedRequests[0].Metadata.TenantId.Should().Be(database.TenantId);

        var job = await database.Context.Set<NotificationDeliveryJob>().SingleAsync();
        job.Status.Should().Be(NotificationDeliveryStatus.Sent);
    }

    [Test]
    public async Task MarkReadAsync_UnreadNotification_MarksReadAndSetsReadAt()
    {
        await using var database = await TestDatabase.CreateAsync();
        var credentialId = Guid.NewGuid();
        var item = CreateInboxItem(database.TenantId, credentialId, "Unread");
        database.Context.Set<NotificationInboxItem>().Add(item);
        await database.Context.SaveChangesAsync();

        var service = CreateService(database.Context);
        var result = await service.MarkReadAsync(new MarkNotificationReadRequest
        {
            TenantId = database.TenantId,
            RecipientCredentialId = credentialId,
            NotificationIds = [item.Id]
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updated = await database.Context.Set<NotificationInboxItem>()
            .AsNoTracking()
            .SingleAsync(notification => notification.Id == item.Id);

        updated.IsRead.Should().BeTrue();
        updated.ReadAt.Should().NotBeNull();
    }

    [Test]
    public async Task RecordDeliveryStatusAsync_ValidTransitions_AdvancesDeliveryStatus()
    {
        await using var database = await TestDatabase.CreateAsync();
        var item = CreateInboxItem(database.TenantId, Guid.NewGuid(), "Delivery");
        database.Context.Set<NotificationInboxItem>().Add(item);
        await database.Context.SaveChangesAsync();

        var service = CreateService(database.Context);

        foreach (var status in new[]
                 {
                     NotificationDeliveryStatus.Pending,
                     NotificationDeliveryStatus.Queued,
                     NotificationDeliveryStatus.Sent,
                     NotificationDeliveryStatus.Delivered
                 })
        {
            var result = await service.RecordDeliveryStatusAsync(new RecordNotificationDeliveryStatusRequest
            {
                TenantId = database.TenantId,
                NotificationInboxItemId = item.Id,
                Channel = NotificationDeliveryChannel.Email,
                Status = status,
                AttemptNumber = 1
            }, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Data!.Status.Should().Be(status);
        }

        var record = await database.Context.Set<NotificationDeliveryStatusRecord>()
            .AsNoTracking()
            .SingleAsync(status => status.NotificationInboxItemId == item.Id);
        record.Status.Should().Be(NotificationDeliveryStatus.Delivered);
    }

    [Test]
    public async Task RecordDeliveryStatusAsync_TerminalDeliveredToPending_ReturnsConflict()
    {
        await using var database = await TestDatabase.CreateAsync();
        var item = CreateInboxItem(database.TenantId, Guid.NewGuid(), "Terminal");
        database.Context.Set<NotificationInboxItem>().Add(item);
        database.Context.Set<NotificationDeliveryStatusRecord>().Add(new NotificationDeliveryStatusRecord
        {
            Id = Guid.NewGuid(),
            TenantId = database.TenantId,
            NotificationInboxItemId = item.Id,
            Channel = NotificationDeliveryChannel.Email,
            Status = NotificationDeliveryStatus.Delivered,
            AttemptNumber = 1,
            RecordedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid(),
            IsEnabled = true
        });
        await database.Context.SaveChangesAsync();

        var service = CreateService(database.Context);
        var result = await service.RecordDeliveryStatusAsync(new RecordNotificationDeliveryStatusRequest
        {
            TenantId = database.TenantId,
            NotificationInboxItemId = item.Id,
            Channel = NotificationDeliveryChannel.Email,
            Status = NotificationDeliveryStatus.Pending
        }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        result.Message.Should().Contain("Cannot transition");
    }

    private static NotificationService CreateService(AppDbContext db) =>
        new(db, NullLogger<NotificationService>.Instance);

    private static void EnableChannels(
        AppDbContext db,
        Guid tenantId,
        Guid credentialId,
        NotificationDeliveryChannel channels)
    {
        db.Set<NotificationPreference>().Add(new NotificationPreference
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CredentialId = credentialId,
            EnabledChannels = channels,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid(),
            IsEnabled = true
        });
        db.SaveChanges();
    }

    private sealed class TestSmsGatewayServiceWrapper : ISmsGatewayServiceWrapper
    {
        public List<CreateSmsMessageRequest> CreatedRequests { get; } = [];

        public Task<CmdResponse> CreateSmsMessage(CreateSmsMessageRequest request)
        {
            CreatedRequests.Add(request);
            return Task.FromResult(new CmdResponse { HttpStatusCode = System.Net.HttpStatusCode.OK });
        }

        public Task<QueryResponse<List<SmsNodeJob>>> GetPendingSmsMessageList(GetPendingSmsMessageListRequest request) =>
            throw new NotSupportedException();

        public Task<QueryResponse<List<SmsNodeJob>>> GetScheduledSmsMessageList(GetScheduledSmsMessageListRequest request) =>
            throw new NotSupportedException();
    }

    private static NotificationInboxItem CreateInboxItem(Guid tenantId, Guid credentialId, string title) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        RecipientCredentialId = credentialId,
        TemplateKey = NotificationTemplateKeys.SystemGeneric,
        Title = title,
        Body = "Body",
        DeliveryChannels = NotificationDeliveryChannel.InApp,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid(),
        IsEnabled = true
    };

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private TestDatabase(SqliteConnection connection, AppDbContext context, Guid tenantId)
        {
            this.connection = connection;
            Context = context;
            TenantId = tenantId;
        }

        public AppDbContext Context { get; }
        public Guid TenantId { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var tenantId = Guid.NewGuid();
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            connection.CreateFunction("now", () => DateTime.UtcNow);
            connection.CreateFunction("uuid_generate_v4", () => Guid.NewGuid());

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Tenant:DefaultId"] = tenantId.ToString()
                })
                .Build();

            var context = new AppDbContext(options, new Microsoft.AspNetCore.Http.HttpContextAccessor(), configuration);

            _ = typeof(NotificationInboxItem).Assembly;
            await context.Database.EnsureCreatedAsync();

            return new TestDatabase(connection, context, tenantId);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
