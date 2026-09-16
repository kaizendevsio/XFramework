using System.Net;
using Communications.Api.Services;
using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Notifications.Domain.Shared.Contracts;
using Notifications.Domain.Shared.Contracts.Requests;
using Notifications.Domain.Shared.Contracts.Responses;
using Notifications.Domain.Shared.Enums;
using Notifications.Integration.Drivers;
using NUnit.Framework;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.BusinessObjects;

namespace Communications.Tests.Services;

/// <summary>
/// The message half of the notification story. Calls push straight from the Yap gateway and never
/// reach this code, so nothing here is exercised by a working call - these tests are what stands
/// between "calls ring" and "messages are silent".
/// </summary>
public sealed class NotificationFanoutTests
{
    [Test]
    public async Task MessageCreated_RequestsPushForEveryEligibleMember()
    {
        await using var harness = await FanoutHarness.CreateAsync();
        var sender = harness.AddMember();
        var reader = harness.AddMember();
        var muted = harness.AddMember(isMuted: true);
        var message = harness.AddMessage(sender);
        await harness.SaveAsync();

        await harness.Fanout.CreateNotificationsAsync(harness.OutboxEvent(MessageRealtimeEvents.MessageCreated, message.Id, sender.CredentialId));

        var requested = harness.Notifications.Requests;
        Assert.That(requested.Select(x => x.RecipientCredentialId), Is.EquivalentTo(new[] { reader.CredentialId }),
            "the sender and the muted member must not be notified");
        Assert.That(requested.Single().DeliveryChannels.HasFlag(NotificationDeliveryChannel.Push), Is.True,
            "push is the only channel that reaches a closed PWA");
        Assert.That(requested.Single().TemplateKey, Is.EqualTo(NotificationTemplateKeys.MessageReceived));
        Assert.That(requested.Single().Data!["ThreadId"], Is.EqualTo(harness.ThreadId.ToString()),
            "the routing identifier the service worker needs to open the conversation");
    }

    [Test]
    public async Task MessageCreated_WhenTheTenantHasNotificationsDisabled_NotifiesNobody()
    {
        await using var harness = await FanoutHarness.CreateAsync(notificationsEnabled: false);
        var sender = harness.AddMember();
        harness.AddMember();
        var message = harness.AddMessage(sender);
        await harness.SaveAsync();

        await harness.Fanout.CreateNotificationsAsync(harness.OutboxEvent(MessageRealtimeEvents.MessageCreated, message.Id, sender.CredentialId));

        Assert.That(harness.Notifications.Requests, Is.Empty);
    }

    [Test]
    public async Task ReadReceiptEvents_NeverReachTheNotificationsModule()
    {
        await using var harness = await FanoutHarness.CreateAsync();
        var sender = harness.AddMember();
        harness.AddMember();
        var message = harness.AddMessage(sender);
        await harness.SaveAsync();

        await harness.Fanout.CreateNotificationsAsync(harness.OutboxEvent(MessageRealtimeEvents.MessagesRead, message.Id, sender.CredentialId));
        await harness.Fanout.CreateNotificationsAsync(harness.OutboxEvent(MessageRealtimeEvents.MessagesDelivered, message.Id, sender.CredentialId));

        Assert.That(harness.Notifications.Requests, Is.Empty);
        Assert.That(harness.Features.Lookups, Is.Zero, "chatter must not cost a feature lookup per event");
    }

    private sealed class FanoutHarness : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly FanoutDb db;

        private FanoutHarness(SqliteConnection connection, FanoutDb db, bool notificationsEnabled)
        {
            this.connection = connection;
            this.db = db;
            TenantId = Guid.NewGuid();
            ThreadId = Guid.NewGuid();
            Features = new StubFeatureService(notificationsEnabled);
            Notifications = new RecordingNotificationsWrapper();
            Fanout = new CommunicationsNotificationFanout(
                db, Features, Notifications, NullLogger<CommunicationsNotificationFanout>.Instance);
        }

        public Guid TenantId { get; }
        public Guid ThreadId { get; }
        public StubFeatureService Features { get; }
        public RecordingNotificationsWrapper Notifications { get; }
        public CommunicationsNotificationFanout Fanout { get; }

        public static async Task<FanoutHarness> CreateAsync(bool notificationsEnabled = true)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            connection.CreateFunction("now", () => DateTime.UtcNow);
            connection.CreateFunction("uuid_generate_v4", () => Guid.NewGuid());

            var harness = new FanoutHarness(
                connection,
                new FanoutDb(new DbContextOptionsBuilder<FanoutDb>().UseSqlite(connection).Options),
                notificationsEnabled);
            await harness.db.Database.EnsureCreatedAsync();
            return harness;
        }

        public MessageThreadMember AddMember(bool isMuted = false)
        {
            var member = new MessageThreadMember
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                MessageThreadId = ThreadId,
                CredentialId = Guid.NewGuid(),
                GroupId = Guid.NewGuid(),
                Alias = string.Empty,
                Emoji = string.Empty,
                Description = string.Empty,
                Status = 1,
                Role = MessageThreadMemberRoles.Member,
                IsMuted = isMuted,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            };
            db.Add(member);
            return member;
        }

        public Message AddMessage(MessageThreadMember sender)
        {
            var message = new Message
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                MessageThreadId = ThreadId,
                MessageThreadMemberId = sender.Id,
                Text = string.Empty,
                MentionedCredentialIdsJson = "[]",
                TemplateVariablesJson = "{}",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
            };
            db.Add(message);
            return message;
        }

        public Task SaveAsync() => db.SaveChangesAsync();

        public MessageOutboxEvent OutboxEvent(string eventType, Guid aggregateId, Guid actorCredentialId) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            EventType = eventType,
            AggregateType = nameof(Message),
            AggregateId = aggregateId,
            ThreadId = ThreadId,
            ActorCredentialId = actorCredentialId,
            PayloadJson = "{}",
            OccurredAt = DateTime.UtcNow,
            IsEnabled = true
        };

        public async ValueTask DisposeAsync()
        {
            await db.DisposeAsync();
            await connection.DisposeAsync();
        }

        /// <summary>Only the tables the fan-out reads, so the rules can be tested without PostgreSQL.</summary>
        private sealed class FanoutDb(DbContextOptions<FanoutDb> options) : DbContext(options)
        {
            protected override void OnModelCreating(ModelBuilder model)
            {
                model.Entity<MessageThreadMember>().Ignore(x => x.Group).Ignore(x => x.Credential)
                    .Ignore(x => x.MessageDeliveries).Ignore(x => x.MessageThread)
                    .Ignore(x => x.MessageThreadMemberRoles).Ignore(x => x.Messages);
                model.Entity<Message>().Ignore(x => x.MessageThread).Ignore(x => x.MessageThreadMember)
                    .Ignore(x => x.MessageDeliveries).Ignore(x => x.MessageReactions)
                    .Ignore(x => x.ParentMessage).Ignore(x => x.Replies).Ignore(x => x.MessageFiles);
                model.Entity<MessageBlock>();
                model.Entity<MessageThreadInvite>();
                model.Entity<MessageReaction>().Ignore(x => x.Message).Ignore(x => x.MessageThreadMember);
            }
        }
    }

    private sealed class StubFeatureService(bool enabled) : ITenantModuleFeatureService
    {
        public int Lookups { get; private set; }

        public Task<Result<bool>> IsEnabledAsync(Guid tenantId, string moduleKey, string? subFeatureKey = null, CancellationToken ct = default)
        {
            Lookups++;
            return Task.FromResult(Result<bool>.Success(enabled));
        }

        public Task<Result> EnsureEnabledAsync(Guid tenantId, string moduleKey, string? subFeatureKey = null, CancellationToken ct = default)
        {
            Lookups++;
            return Task.FromResult(enabled ? Result.Success() : Result.Forbidden("Feature disabled"));
        }

        public void Invalidate(Guid tenantId, string moduleKey, string? subFeatureKey = null)
        {
        }
    }

    private sealed class RecordingNotificationsWrapper : INotificationsServiceWrapper
    {
        public List<CreateNotificationRequest> Requests { get; } = [];

        public Task<QueryResponse<NotificationInboxItemResponse>> CreateNotification(CreateNotificationRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            return Task.FromResult(new QueryResponse<NotificationInboxItemResponse> { HttpStatusCode = HttpStatusCode.Created });
        }

        public void Initialize()
        {
        }

        public Task<QueryResponse<GetNotificationInboxResponse>> GetNotificationInbox(GetNotificationInboxRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CmdResponse> MarkNotificationRead(MarkNotificationReadRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<QueryResponse<NotificationPreferencesResponse>> UpdateNotificationPreferences(UpdateNotificationPreferencesRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<QueryResponse<NotificationDeliveryStatusResponse>> RecordNotificationDeliveryStatus(RecordNotificationDeliveryStatusRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<QueryResponse<PushConfigurationResponse>> GetPushConfiguration(GetPushConfigurationRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<QueryResponse<PushSubscriptionResponse>> RegisterPushSubscription(RegisterPushSubscriptionRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CmdResponse> RemovePushSubscription(RemovePushSubscriptionRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<QueryResponse<SendDirectPushResponse>> SendDirectPush(SendDirectPushRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
