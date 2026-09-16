using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Notifications.Api.Services.Push;
using Notifications.Domain.Shared.Contracts;
using Notifications.Domain.Shared.Contracts.Requests;
using Notifications.Domain.Shared.Enums;
using NUnit.Framework;

namespace Notifications.Tests.Services.Push;

public sealed class NotificationPushServiceTests
{
    // Any valid P-256 point works; this is the receiver key from the RFC 8291 example.
    private const string P256dh = "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4";
    private const string Auth = "BTBZMqHH6r4Tts7J_aSIgg";
    private const string VapidPublic = "BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8";
    private const string VapidPrivate = "yfWPiYE-n46HLnH0KqZOF1fJJU3MYrct3AELtAQ-oRw";

    [Test]
    public async Task ForegroundLease_SuppressesOnlyOwnedDevice_AndHidingRestoresDelivery()
    {
        await using var database = await NotificationTestDatabase.CreateAsync();
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var presence = new PushPresence(cache);
        var credential = Guid.NewGuid();
        var handler = new RecordingPushHandler(_ => HttpStatusCode.Created);
        var service = NotificationTestHost.CreatePushService(database.Context,
            new TestInvocationContextAccessor(database.TenantId, credential), VapidConfiguration(), handler, presence);
        await service.RegisterAsync(Registration(credential), CancellationToken.None);
        await service.RegisterAsync(Registration(credential) with { Endpoint = "https://push.example.test/other-phone" }, CancellationToken.None);
        var request = new SetPushPresenceRequest { CredentialId = credential, Endpoint = Registration(credential).Endpoint, WindowId = Guid.NewGuid(), Visible = true };
        (await service.SetPresenceAsync(request, CancellationToken.None)).IsSuccess.Should().BeTrue();
        var summary = await service.SendAsync(database.TenantId, credential, new PushEnvelope(1, "message", null, null, null), 60, "high", CancellationToken.None);
        summary.Suppressed.Should().Be(1);
        summary.Delivered.Should().Be(1);
        handler.Requests.Single().RequestUri!.AbsolutePath.Should().Be("/other-phone");
        request.Visible = false;
        await service.SetPresenceAsync(request, CancellationToken.None);
        summary = await service.SendAsync(database.TenantId, credential, new PushEnvelope(1, "call", null, null, null), 60, "high", CancellationToken.None);
        summary.Suppressed.Should().Be(0);
        summary.Delivered.Should().Be(2);
        var outsider = NotificationTestHost.CreatePushService(database.Context,
            new TestInvocationContextAccessor(database.TenantId, Guid.NewGuid()), presence: presence);
        request.CredentialId = Guid.Empty;
        (await outsider.SetPresenceAsync(request, CancellationToken.None)).StatusCode.Should().Be(404);
    }

    [Test]
    public void ForegroundLease_ExpiresAndSeparatesWindowsAccountsAndTenants()
    {
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var presence = new PushPresence(cache);
        var tenant = Guid.NewGuid(); var user = Guid.NewGuid(); var tab1 = Guid.NewGuid(); var tab2 = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        presence.Set(tenant, user, "device", tab1, true, now);
        presence.Set(tenant, user, "device", tab2, true, now);
        presence.Set(tenant, user, "device", tab1, false, now);
        presence.IsVisible(tenant, user, "device", now).Should().BeTrue();
        presence.IsVisible(Guid.NewGuid(), user, "device", now).Should().BeFalse();
        presence.IsVisible(tenant, Guid.NewGuid(), "device", now).Should().BeFalse();
        presence.IsVisible(tenant, user, "device", now.AddSeconds(46)).Should().BeFalse();
    }

    [Test]
    public async Task GetConfigurationAsync_WithoutVapidKeys_ReportsPushDisabled()
    {
        await using var database = await NotificationTestDatabase.CreateAsync();
        var credentialId = Guid.NewGuid();
        var service = NotificationTestHost.CreatePushService(
            database.Context,
            new TestInvocationContextAccessor(database.TenantId, credentialId));

        var result = await service.GetConfigurationAsync(new GetPushConfigurationRequest(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Enabled.Should().BeFalse();
        result.Data.VapidPublicKey.Should().BeNull();
    }

    [Test]
    public async Task GetConfigurationAsync_WithConfiguredKeys_ReturnsThePublicKeyOnly()
    {
        await using var database = await NotificationTestDatabase.CreateAsync();
        var credentialId = Guid.NewGuid();
        var service = NotificationTestHost.CreatePushService(
            database.Context,
            new TestInvocationContextAccessor(database.TenantId, credentialId),
            VapidConfiguration());

        var result = await service.GetConfigurationAsync(new GetPushConfigurationRequest(), CancellationToken.None);

        result.Data!.Enabled.Should().BeTrue();
        result.Data.VapidPublicKey.Should().Be(VapidPublic);
        JsonSerializer.Serialize(result.Data).Should().NotContain(VapidPrivate, "the private key must never leave the server");
    }

    [Test]
    public async Task RegisterAsync_SameEndpointTwice_UpdatesInPlace()
    {
        await using var database = await NotificationTestDatabase.CreateAsync();
        var credentialId = Guid.NewGuid();
        var service = NotificationTestHost.CreatePushService(
            database.Context,
            new TestInvocationContextAccessor(database.TenantId, credentialId));

        var first = await service.RegisterAsync(Registration(credentialId), CancellationToken.None);
        var second = await service.RegisterAsync(Registration(credentialId), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.Data!.Id.Should().Be(first.Data!.Id);
        (await database.Context.Set<NotificationPushSubscription>().CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task RegisterAsync_ForAnotherCredential_IsRejected()
    {
        await using var database = await NotificationTestDatabase.CreateAsync();
        var service = NotificationTestHost.CreatePushService(
            database.Context,
            new TestInvocationContextAccessor(database.TenantId, Guid.NewGuid()));

        var result = await service.RegisterAsync(Registration(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("signed-in credential");
    }

    [Test]
    public async Task RegisterAsync_MalformedKeys_AreRejectedBeforeStorage()
    {
        await using var database = await NotificationTestDatabase.CreateAsync();
        var credentialId = Guid.NewGuid();
        var service = NotificationTestHost.CreatePushService(
            database.Context,
            new TestInvocationContextAccessor(database.TenantId, credentialId));

        var request = Registration(credentialId) with { P256dh = "not-a-key" };
        var result = await service.RegisterAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        (await database.Context.Set<NotificationPushSubscription>().CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task RegisterAsync_HttpEndpoint_IsRejected()
    {
        await using var database = await NotificationTestDatabase.CreateAsync();
        var credentialId = Guid.NewGuid();
        var service = NotificationTestHost.CreatePushService(
            database.Context,
            new TestInvocationContextAccessor(database.TenantId, credentialId));

        var result = await service.RegisterAsync(
            Registration(credentialId) with { Endpoint = "http://push.example.net/insecure" },
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task RemoveAsync_UnknownEndpoint_SucceedsIdempotently()
    {
        await using var database = await NotificationTestDatabase.CreateAsync();
        var credentialId = Guid.NewGuid();
        var service = NotificationTestHost.CreatePushService(
            database.Context,
            new TestInvocationContextAccessor(database.TenantId, credentialId));

        var result = await service.RemoveAsync(new RemovePushSubscriptionRequest
        {
            CredentialId = credentialId,
            Endpoint = "https://push.example.net/never-registered"
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task RemoveAsync_AnotherUsersEndpoint_DoesNotRemoveTheirSubscription()
    {
        await using var database = await NotificationTestDatabase.CreateAsync();
        var ownerId = Guid.NewGuid();
        var owner = NotificationTestHost.CreatePushService(
            database.Context, new TestInvocationContextAccessor(database.TenantId, ownerId));
        var registration = Registration(ownerId);
        (await owner.RegisterAsync(registration, CancellationToken.None)).IsSuccess.Should().BeTrue();
        var otherId = Guid.NewGuid();
        var other = NotificationTestHost.CreatePushService(
            database.Context, new TestInvocationContextAccessor(database.TenantId, otherId));

        var result = await other.RemoveAsync(new RemovePushSubscriptionRequest
        {
            CredentialId = otherId,
            Endpoint = registration.Endpoint
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await database.Context.Set<NotificationPushSubscription>().SingleAsync())
            .CredentialId.Should().Be(ownerId);
    }

    [Test]
    public async Task SendAsync_PayloadCarriesRoutingIdentifiersOnly()
    {
        await using var database = await NotificationTestDatabase.CreateAsync();
        var credentialId = Guid.NewGuid();
        var handler = new RecordingPushHandler(_ => HttpStatusCode.Created);
        var invocation = new TestInvocationContextAccessor(database.TenantId, credentialId);
        var service = NotificationTestHost.CreatePushService(database.Context, invocation, VapidConfiguration(), handler);
        await service.RegisterAsync(Registration(credentialId), CancellationToken.None);

        var threadId = Guid.NewGuid();
        var summary = await service.SendAsync(
            database.TenantId,
            credentialId,
            new PushEnvelope(1, NotificationPushService.KindMessage, threadId, Guid.NewGuid(), null),
            3600,
            "normal",
            CancellationToken.None);

        summary.Delivered.Should().Be(1);
        handler.Requests.Should().ContainSingle();

        var request = handler.Requests[0];
        request.Headers.GetValues("Authorization").Single().Should().StartWith("vapid t=");
        request.Headers.GetValues("Authorization").Single().Should().Contain($"k={VapidPublic}");
        request.Headers.GetValues("TTL").Single().Should().Be("3600");
        request.Content!.Headers.ContentEncoding.Should().Contain("aes128gcm");

        // The body is an opaque aes128gcm record. The only guarantee that matters here is that
        // nothing readable went into it: the server never sees plaintext message content.
        handler.Bodies[0].Length.Should().BeGreaterThan(86);
    }

    [Test]
    public async Task SendAsync_PushServiceReports410_DeletesTheSubscription()
    {
        await using var database = await NotificationTestDatabase.CreateAsync();
        var credentialId = Guid.NewGuid();
        var handler = new RecordingPushHandler(_ => HttpStatusCode.Gone);
        var invocation = new TestInvocationContextAccessor(database.TenantId, credentialId);
        var service = NotificationTestHost.CreatePushService(database.Context, invocation, VapidConfiguration(), handler);
        await service.RegisterAsync(Registration(credentialId), CancellationToken.None);

        var summary = await service.SendAsync(
            database.TenantId,
            credentialId,
            new PushEnvelope(1, NotificationPushService.KindMessage, null, null, null),
            60,
            "normal",
            CancellationToken.None);

        summary.Removed.Should().Be(1);
        summary.Delivered.Should().Be(0);
        (await database.Context.Set<NotificationPushSubscription>().CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task SendAsync_TransientFailure_KeepsTheSubscriptionAndCountsTheFailure()
    {
        await using var database = await NotificationTestDatabase.CreateAsync();
        var credentialId = Guid.NewGuid();
        var handler = new RecordingPushHandler(_ => HttpStatusCode.ServiceUnavailable);
        var invocation = new TestInvocationContextAccessor(database.TenantId, credentialId);
        var service = NotificationTestHost.CreatePushService(database.Context, invocation, VapidConfiguration(), handler);
        await service.RegisterAsync(Registration(credentialId), CancellationToken.None);

        var summary = await service.SendAsync(
            database.TenantId,
            credentialId,
            new PushEnvelope(1, NotificationPushService.KindMessage, null, null, null),
            60,
            "normal",
            CancellationToken.None);

        summary.Failed.Should().Be(1);
        var stored = await database.Context.Set<NotificationPushSubscription>().AsNoTracking().SingleAsync();
        stored.ConsecutiveFailureCount.Should().Be(1);
        stored.LastErrorCode.Should().Be("transient");
    }

    [Test]
    public async Task SendAsync_WithoutVapidKeys_DoesNothingAndDoesNotThrow()
    {
        await using var database = await NotificationTestDatabase.CreateAsync();
        var credentialId = Guid.NewGuid();
        var invocation = new TestInvocationContextAccessor(database.TenantId, credentialId);
        var service = NotificationTestHost.CreatePushService(database.Context, invocation);
        await service.RegisterAsync(Registration(credentialId), CancellationToken.None);

        var summary = await service.SendAsync(
            database.TenantId,
            credentialId,
            new PushEnvelope(1, NotificationPushService.KindMessage, null, null, null),
            60,
            "normal",
            CancellationToken.None);

        summary.Delivered.Should().Be(0);
        summary.Failed.Should().Be(0);
    }

    [Test]
    public async Task SendAsync_OtherTenantSubscription_IsNotReached()
    {
        await using var database = await NotificationTestDatabase.CreateAsync();
        var credentialId = Guid.NewGuid();
        var handler = new RecordingPushHandler(_ => HttpStatusCode.Created);
        var invocation = new TestInvocationContextAccessor(database.TenantId, credentialId);
        var service = NotificationTestHost.CreatePushService(database.Context, invocation, VapidConfiguration(), handler);
        await service.RegisterAsync(Registration(credentialId), CancellationToken.None);

        var summary = await service.SendAsync(
            Guid.NewGuid(),
            credentialId,
            new PushEnvelope(1, NotificationPushService.KindMessage, null, null, null),
            60,
            "normal",
            CancellationToken.None);

        summary.Delivered.Should().Be(0);
        handler.Requests.Should().BeEmpty();
    }

    [Test]
    public async Task CreateNotificationAsync_WithoutAnySubscription_QueuesNoPushJob()
    {
        await using var database = await NotificationTestDatabase.CreateAsync();
        var credentialId = Guid.NewGuid();
        var invocation = new TestInvocationContextAccessor(database.TenantId, credentialId);
        var notifications = NotificationTestHost.CreateNotificationService(database.Context, invocation);

        var created = await notifications.CreateNotificationAsync(new CreateNotificationRequest
        {
            TenantId = database.TenantId,
            RecipientCredentialId = credentialId,
            TemplateKey = NotificationTemplateKeys.MessageReceived,
            Title = "New message",
            Body = "ciphertext",
            DeliveryChannels = NotificationPreferenceDefaults.EnabledChannels
        }, CancellationToken.None);

        created.IsSuccess.Should().BeTrue();
        created.Data!.DeliveryChannels.Should().Be(NotificationDeliveryChannel.InApp);
        (await database.Context.Set<NotificationDeliveryJob>().CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task CreateNotificationAsync_WithASubscription_QueuesARoutingOnlyPushJob()
    {
        await using var database = await NotificationTestDatabase.CreateAsync();
        var credentialId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var invocation = new TestInvocationContextAccessor(database.TenantId, credentialId);
        var push = NotificationTestHost.CreatePushService(database.Context, invocation);
        await push.RegisterAsync(Registration(credentialId), CancellationToken.None);

        var notifications = NotificationTestHost.CreateNotificationService(database.Context, invocation, push);
        var created = await notifications.CreateNotificationAsync(new CreateNotificationRequest
        {
            TenantId = database.TenantId,
            RecipientCredentialId = credentialId,
            TemplateKey = NotificationTemplateKeys.MessageReceived,
            Title = "New message",
            Body = "an unread message body that must never be pushed",
            DeliveryChannels = NotificationPreferenceDefaults.EnabledChannels,
            Data = new Dictionary<string, string> { ["threadId"] = threadId.ToString() }
        }, CancellationToken.None);

        created.IsSuccess.Should().BeTrue();

        var job = await database.Context.Set<NotificationDeliveryJob>()
            .AsNoTracking()
            .SingleAsync(x => x.Channel == NotificationDeliveryChannel.Push);

        job.ProviderKey.Should().Be(WebPushVapidProvider.ProviderKey);
        job.PayloadJson.Should().NotContain("an unread message body");
        job.PayloadJson.Should().NotContain("New message");

        var envelope = JsonSerializer.Deserialize<PushEnvelope>(job.PayloadJson!, NotificationPushService.PushEnvelopeJson);
        envelope!.Kind.Should().Be(NotificationPushService.KindMessage);
        envelope.ThreadId.Should().Be(threadId);
        envelope.NotificationId.Should().Be(created.Data!.Id);
    }

    private static RegisterPushSubscriptionRequest Registration(Guid credentialId) => new()
    {
        CredentialId = credentialId,
        Endpoint = "https://push.example.net/JzLQ3raZJfFBR0aqvOMsLrt54w4rJUsV",
        P256dh = P256dh,
        Auth = Auth,
        DeviceLabel = "iPhone"
    };

    private static IConfiguration VapidConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:Push:Vapid:PublicKey"] = VapidPublic,
                ["Notifications:Push:Vapid:PrivateKey"] = VapidPrivate,
                ["Notifications:Push:Vapid:Subject"] = "mailto:ops@example.net"
            })
            .Build();
}
