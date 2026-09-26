using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using Moq;
using NUnit.Framework;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Client.Tests;

// The reported outage showed "Can't reach Yap. Reconnecting…" for a sign-in that had ended,
// which also disabled the Sign in button. An ended sign-in and a lost network are different
// states: only the first asks the person to sign in, and only the second is offline.
public sealed class SessionEndedTests
{
    [Test]
    public async Task SessionEndedSignal_RequiresSignIn_AndStaysOnline()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Recipient");
        await fixture.Store.SetSettingAsync("user", JsonSerializer.Serialize(user));
        var requests = new List<string>();
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(true);
        // The session probe still answers with the saved account, as it did during the outage:
        // the explicit signal alone must be enough.
        using var http = new HttpClient(new Handler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            requests.Add(path);
            if (path == "/api/session") return Json(new SessionResponse(user, "token"));
            var ended = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            ended.Headers.Add("X-Yap-Session", "ended");
            return ended;
        })) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);

        await state.InitializeAsync();
        await WaitForAsync(() => state.NeedsLogin || !state.Online);

        Assert.Multiple(() =>
        {
            Assert.That(state.NeedsLogin, Is.True, "An ended sign-in asks the person to sign in again.");
            Assert.That(state.Online, Is.True, "The server answered; the device is not offline.");
            Assert.That(state.Error, Is.Null, "The sign-in notice says it; a second error toast would repeat it.");
            Assert.That(state.User, Is.EqualTo(user), "Saved conversations stay readable on the device.");
        });

        // Nothing guarded by the sign-in keeps asking the server once it ended.
        requests.Clear();
        await state.RefreshHint();
        await state.SelectAsync(Guid.NewGuid());
        Assert.That(requests.Where(path => path.StartsWith("/api/chat/", StringComparison.Ordinal)), Is.Empty);
    }

    [TestCase("network")]
    [TestCase("502")]
    [TestCase("503")]
    [TestCase("timeout")]
    public async Task NetworkFailure_IsOffline_AndNeverSignsOut(string failure)
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Recipient");
        await fixture.Store.SetSettingAsync("user", JsonSerializer.Serialize(user));
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(true);
        using var http = new HttpClient(new Handler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/session") return Json(new SessionResponse(user, "token"));
            return failure switch
            {
                "network" => throw new HttpRequestException("TypeError: Failed to fetch"),
                "timeout" => throw new TaskCanceledException("The request timed out."),
                "502" => new HttpResponseMessage(HttpStatusCode.BadGateway),
                _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            };
        })) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);

        await state.InitializeAsync();
        await WaitForAsync(() => state.NeedsLogin || !state.Online);

        Assert.Multiple(() =>
        {
            Assert.That(state.Online, Is.False, "A deploy or a dropped connection shows the reconnecting notice.");
            Assert.That(state.NeedsLogin, Is.False, "A network failure never signs the person out.");
            Assert.That(state.User, Is.EqualTo(user));
        });
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++) await Task.Delay(10);
    }

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            try { return Task.FromResult(handle(request)); }
            catch (Exception ex) { return Task.FromException<HttpResponseMessage>(ex); }
        }
    }
}
