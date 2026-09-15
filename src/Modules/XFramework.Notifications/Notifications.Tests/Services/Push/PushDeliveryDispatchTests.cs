using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Notifications.Api.Services;
using Notifications.Api.Services.Push;
using Notifications.Domain.Shared.Contracts;
using Notifications.Domain.Shared.Contracts.Requests;
using Notifications.Domain.Shared.Enums;
using NUnit.Framework;
using SmsGateway.Domain.Shared.Contracts.Requests.Create;
using SmsGateway.Domain.Shared.Contracts.Requests.Get;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;
using SmsGateway.Integration.Drivers;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Security;

namespace Notifications.Tests.Services.Push;

/// <summary>Covers the Push arm of the delivery dispatcher end to end, from inbox item to HTTP request.</summary>
public sealed class PushDeliveryDispatchTests
{
    private const string P256dh = "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4";
    private const string Auth = "BTBZMqHH6r4Tts7J_aSIgg";
    private const string VapidPublic = "BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8";
    private const string VapidPrivate = "yfWPiYE-n46HLnH0KqZOF1fJJU3MYrct3AELtAQ-oRw";

    [Test]
    public async Task DispatchDueAsync_DeliversThePushJobAndMarksItSent()
    {
        var handler = new RecordingPushHandler(_ => HttpStatusCode.Created);
        await using var database = await NotificationTestDatabase.CreateAsync();
        var (push, job) = await QueueAsync(database, handler);

        var processed = await Dispatcher(database, push).DispatchDueAsync(CancellationToken.None);

        processed.Should().Be(1);
        handler.Requests.Should().ContainSingle();
        // An aes128gcm record is header(21) + sender point(65) + ciphertext + tag(16); anything
        // shorter would mean the payload never got encrypted.
        handler.Bodies[0].Length.Should().BeGreaterThan(102);
        handler.Bodies[0][20].Should().Be(65);

        var stored = await database.Context.Set<NotificationDeliveryJob>().AsNoTracking().SingleAsync(x => x.Id == job);
        stored.Status.Should().Be(NotificationDeliveryStatus.Sent);
        stored.ProviderMessageId.Should().Be("web-push:1");
    }

    [Test]
    public async Task DispatchDueAsync_WhenEveryEndpointIsGone_FailsWithoutRetrying()
    {
        var handler = new RecordingPushHandler(_ => HttpStatusCode.Gone);
        await using var database = await NotificationTestDatabase.CreateAsync();
        var (push, job) = await QueueAsync(database, handler);

        await Dispatcher(database, push).DispatchDueAsync(CancellationToken.None);

        var stored = await database.Context.Set<NotificationDeliveryJob>().AsNoTracking().SingleAsync(x => x.Id == job);
        stored.Status.Should().Be(NotificationDeliveryStatus.Failed);
        stored.LastErrorCode.Should().Be("push-no-subscriptions");
        stored.CompletedAt.Should().NotBeNull();
        (await database.Context.Set<NotificationPushSubscription>().CountAsync()).Should().Be(0);

        // A deleted subscription can never accept a retry, so the finished job must not be leased again.
        (await Dispatcher(database, push).DispatchDueAsync(CancellationToken.None)).Should().Be(0);
        handler.Requests.Should().ContainSingle();
    }

    [Test]
    public async Task DispatchDueAsync_TransientPushFailure_IsScheduledForAnotherAttempt()
    {
        var handler = new RecordingPushHandler(_ => HttpStatusCode.InternalServerError);
        await using var database = await NotificationTestDatabase.CreateAsync();
        var (push, job) = await QueueAsync(database, handler);

        await Dispatcher(database, push).DispatchDueAsync(CancellationToken.None);

        var stored = await database.Context.Set<NotificationDeliveryJob>().AsNoTracking().SingleAsync(x => x.Id == job);
        stored.Status.Should().Be(NotificationDeliveryStatus.Queued);
        stored.LastErrorCode.Should().Be("push-send-failed");
        stored.NextAttemptAt.Should().NotBeNull();
        (await database.Context.Set<NotificationPushSubscription>().CountAsync()).Should().Be(1);
    }

    private static async Task<(NotificationPushService Push, Guid JobId)> QueueAsync(
        NotificationTestDatabase database,
        RecordingPushHandler handler)
    {
        var credentialId = Guid.NewGuid();
        var invocation = new TestInvocationContextAccessor(database.TenantId, credentialId);
        var push = NotificationTestHost.CreatePushService(database.Context, invocation, VapidConfiguration(), handler);
        await push.RegisterAsync(new RegisterPushSubscriptionRequest
        {
            CredentialId = credentialId,
            Endpoint = "https://push.example.net/JzLQ3raZJfFBR0aqvOMsLrt54w4rJUsV",
            P256dh = P256dh,
            Auth = Auth
        }, CancellationToken.None);

        var created = await NotificationTestHost.CreateNotificationService(database.Context, invocation, push)
            .CreateNotificationAsync(new CreateNotificationRequest
            {
                TenantId = database.TenantId,
                RecipientCredentialId = credentialId,
                TemplateKey = NotificationTemplateKeys.MessageReceived,
                Title = "New message",
                Body = "ciphertext the server cannot read",
                DeliveryChannels = NotificationDeliveryChannel.Push
            }, CancellationToken.None);
        created.IsSuccess.Should().BeTrue();

        var job = await database.Context.Set<NotificationDeliveryJob>()
            .AsNoTracking()
            .SingleAsync(x => x.Channel == NotificationDeliveryChannel.Push);
        return (push, job.Id);
    }

    private static NotificationDeliveryDispatcher Dispatcher(NotificationTestDatabase database, NotificationPushService push) =>
        new(
            database.Context,
            new UnusedSmsGateway(),
            push,
            NullLogger<NotificationDeliveryDispatcher>.Instance,
            VapidConfiguration());

    private static IConfiguration VapidConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:Push:Vapid:PublicKey"] = VapidPublic,
                ["Notifications:Push:Vapid:PrivateKey"] = VapidPrivate,
                ["Notifications:Push:Vapid:Subject"] = "mailto:ops@example.net"
            })
            .Build();

    private sealed class UnusedSmsGateway : ISmsGatewayServiceWrapper
    {
        public Task<CmdResponse> CreateSmsMessage(CreateSmsMessageRequest request) => throw new NotSupportedException();
        public Task<QueryResponse<List<SmsNodeJob>>> GetPendingSmsMessageList(GetPendingSmsMessageListRequest request) => throw new NotSupportedException();
        public Task<QueryResponse<List<SmsNodeJob>>> GetScheduledSmsMessageList(GetScheduledSmsMessageListRequest request) => throw new NotSupportedException();
    }
}
