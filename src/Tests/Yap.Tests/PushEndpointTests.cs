using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using Notifications.Domain.Shared.Contracts.Requests;
using Notifications.Domain.Shared.Contracts.Responses;
using Notifications.Integration.Drivers;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;
using Yap.Contracts;

namespace Yap.Tests;

[TestFixture]
public sealed class PushEndpointTests
{
    private const string VapidPublic = "BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8";

    [Test]
    public async Task PushEndpoints_RequireASignedInSessionAndTheAntiforgeryToken()
    {
        var notifications = Notifications();
        await using var app = UiFixture.Create(0, configureServices: services =>
            services.Replace(ServiceDescriptor.Singleton(notifications.Object)));
        await app.StartAsync();
        using var client = Client(app);

        Assert.That((await client.GetAsync("api/chat/push/config")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await client.PostAsJsonAsync("api/chat/push/subscribe", new { })).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var session = await SignInAsync(client);
        Assert.That((await client.GetAsync("api/chat/push/config")).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        client.DefaultRequestHeaders.Remove("RequestVerificationToken");
        Assert.That((await client.PostAsJsonAsync("api/chat/push/unsubscribe", new { endpoint = "https://push.example.net/x" })).StatusCode,
            Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(session.User, Is.Not.Null);
        await app.StopAsync();
    }

    [Test]
    public async Task Config_ReturnsOnlyThePublicApplicationServerKey()
    {
        var notifications = Notifications();
        await using var app = UiFixture.Create(0, configureServices: services =>
            services.Replace(ServiceDescriptor.Singleton(notifications.Object)));
        await app.StartAsync();
        using var client = Client(app);
        await SignInAsync(client);

        var config = await client.GetFromJsonAsync<PushConfigResponse>("api/chat/push/config");

        Assert.That(config!.Enabled, Is.True);
        Assert.That(config.PublicKey, Is.EqualTo(VapidPublic));
        await app.StopAsync();
    }

    [Test]
    public async Task Config_WhenNotificationsIsUnavailable_ReportsPushAsOffRatherThanFailing()
    {
        var notifications = Notifications();
        notifications.Setup(x => x.GetPushConfiguration(It.IsAny<GetPushConfigurationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse<PushConfigurationResponse> { HttpStatusCode = HttpStatusCode.ServiceUnavailable });
        await using var app = UiFixture.Create(0, configureServices: services =>
            services.Replace(ServiceDescriptor.Singleton(notifications.Object)));
        await app.StartAsync();
        using var client = Client(app);
        await SignInAsync(client);

        var config = await client.GetFromJsonAsync<PushConfigResponse>("api/chat/push/config");

        Assert.That(config!.Enabled, Is.False);
        Assert.That(config.PublicKey, Is.Null);
        await app.StopAsync();
    }

    [Test]
    public async Task Subscribe_BindsTheSubscriptionToTheSignedInCredential_NotTheRequestBody()
    {
        RegisterPushSubscriptionRequest? captured = null;
        var notifications = Notifications();
        notifications.Setup(x => x.RegisterPushSubscription(It.IsAny<RegisterPushSubscriptionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RegisterPushSubscriptionRequest request, CancellationToken _) =>
            {
                captured = request;
                return new QueryResponse<PushSubscriptionResponse> { HttpStatusCode = HttpStatusCode.OK, Response = new() { Id = Guid.NewGuid() } };
            });
        await using var app = UiFixture.Create(0, configureServices: services =>
            services.Replace(ServiceDescriptor.Singleton(notifications.Object)));
        await app.StartAsync();
        using var client = Client(app);
        var session = await SignInAsync(client);

        var response = await client.PostAsJsonAsync("api/chat/push/subscribe", new
        {
            // A caller-supplied credential must be ignored: registering someone else's endpoint
            // would leak when they receive messages.
            credentialId = Guid.NewGuid(),
            endpoint = "https://push.example.net/abc",
            p256dh = "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4",
            auth = "BTBZMqHH6r4Tts7J_aSIgg",
            label = "iPhone"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.CredentialId, Is.EqualTo(session.User!.CredentialId));
        Assert.That(captured.Endpoint, Is.EqualTo("https://push.example.net/abc"));
        Assert.That(captured.Metadata.RequestedTenantId, Is.EqualTo(session.User.TenantId));
        await app.StopAsync();
    }

    [Test]
    public async Task Subscribe_WhenNotificationsRejectsTheDevice_ReportsAFailureToTheBrowser()
    {
        var notifications = Notifications();
        notifications.Setup(x => x.RegisterPushSubscription(It.IsAny<RegisterPushSubscriptionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse<PushSubscriptionResponse> { HttpStatusCode = HttpStatusCode.BadRequest });
        await using var app = UiFixture.Create(0, configureServices: services =>
            services.Replace(ServiceDescriptor.Singleton(notifications.Object)));
        await app.StartAsync();
        using var client = Client(app);
        await SignInAsync(client);

        var response = await client.PostAsJsonAsync("api/chat/push/subscribe",
            new { endpoint = "https://push.example.net/abc", p256dh = "x", auth = "y" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        await app.StopAsync();
    }

    [Test]
    public async Task Unsubscribe_ForwardsTheEndpointAndSucceedsEvenWhenItWasAlreadyGone()
    {
        RemovePushSubscriptionRequest? captured = null;
        var notifications = Notifications();
        notifications.Setup(x => x.RemovePushSubscription(It.IsAny<RemovePushSubscriptionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RemovePushSubscriptionRequest request, CancellationToken _) =>
            {
                captured = request;
                return new CmdResponse { HttpStatusCode = HttpStatusCode.NotFound };
            });
        await using var app = UiFixture.Create(0, configureServices: services =>
            services.Replace(ServiceDescriptor.Singleton(notifications.Object)));
        await app.StartAsync();
        using var client = Client(app);
        var session = await SignInAsync(client);

        var response = await client.PostAsJsonAsync("api/chat/push/unsubscribe", new { endpoint = "https://push.example.net/abc" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(captured!.Endpoint, Is.EqualTo("https://push.example.net/abc"));
        Assert.That(captured.CredentialId, Is.EqualTo(session.User!.CredentialId));
        await app.StopAsync();
    }

    private static Mock<INotificationsServiceWrapper> Notifications()
    {
        var notifications = new Mock<INotificationsServiceWrapper>();
        notifications.Setup(x => x.GetPushConfiguration(It.IsAny<GetPushConfigurationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse<PushConfigurationResponse>
            {
                HttpStatusCode = HttpStatusCode.OK,
                Response = new PushConfigurationResponse { Enabled = true, VapidPublicKey = VapidPublic, SubscriptionCount = 0 }
            });
        notifications.Setup(x => x.RegisterPushSubscription(It.IsAny<RegisterPushSubscriptionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse<PushSubscriptionResponse> { HttpStatusCode = HttpStatusCode.OK, Response = new() { Id = Guid.NewGuid() } });
        notifications.Setup(x => x.RemovePushSubscription(It.IsAny<RemovePushSubscriptionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse { HttpStatusCode = HttpStatusCode.OK });
        return notifications;
    }

    private static HttpClient Client(Microsoft.AspNetCore.Builder.WebApplication app) =>
        new(new HttpClientHandler { CookieContainer = new(), AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };

    private static async Task<SessionResponse> SignInAsync(HttpClient client)
    {
        var session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        client.DefaultRequestHeaders.Add("RequestVerificationToken", session.AntiforgeryToken);
        await client.PostAsync("api/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["username"] = "fixture", ["password"] = Environment.GetEnvironmentVariable("YAP_FIXTURE_PASSWORD") ?? "fixture" }));
        session = (await client.GetFromJsonAsync<SessionResponse>("api/session"))!;
        client.DefaultRequestHeaders.Remove("RequestVerificationToken");
        client.DefaultRequestHeaders.Add("RequestVerificationToken", session.AntiforgeryToken);
        client.DefaultRequestHeaders.Add("X-Yap-Account", $"{session.User!.TenantId:N}:{session.User.CredentialId:N}");
        return session;
    }

    private sealed record PushConfigResponse(bool Enabled, string? PublicKey, int Devices);
}
