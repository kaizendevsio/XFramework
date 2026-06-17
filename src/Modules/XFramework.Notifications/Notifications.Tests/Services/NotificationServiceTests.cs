using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Notifications.Api.Services;
using Notifications.Domain.Shared.Contracts;
using Notifications.Domain.Shared.Contracts.Requests;
using Notifications.Domain.Shared.Enums;
using NUnit.Framework;
using XFramework.Domain.Contexts;

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
