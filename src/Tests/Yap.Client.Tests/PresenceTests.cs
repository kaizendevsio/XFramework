using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using Moq;
using NUnit.Framework;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Client.Tests;

public sealed class PresenceTests
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task Presence_OnlyChecksInWhileVisible_AndFailureDoesNotInterruptChat(bool visible, bool fail)
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "You");
        await fixture.Store.SetSettingAsync("user", JsonSerializer.Serialize(user));
        var online = false;
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).Returns(() => ValueTask.FromResult(online));
        js.Setup(x => x.InvokeAsync<bool>("yap.device.visible", It.IsAny<object?[]?>())).ReturnsAsync(visible);
        var handler = new Handler(user, fail);
        using var http = new HttpClient(handler) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync(); online = true;
        await state.SynchronizeAsync();
        Assert.That(handler.Heartbeats, Is.EqualTo(visible ? 1 : 0));
        Assert.That(state.Online, Is.True);
        Assert.That(state.Error, Is.Null);
        await state.SynchronizeAsync();
        Assert.That(handler.Heartbeats, Is.EqualTo(visible ? 1 : 0), "Repeated refreshes do not spam heartbeats");
        var peer = new Person(Guid.NewGuid(), "Peer", "peer", ActiveUntil: DateTime.UtcNow.AddSeconds(40));
        var chat = new Conversation { People = [peer], Members = 2 };
        Assert.That(state.ConversationSubtitle(chat), Is.EqualTo("Active now"));
        chat.People = [peer with { ActiveUntil = DateTime.UtcNow.AddSeconds(-1) }];
        Assert.That(state.ConversationSubtitle(chat), Is.EqualTo("Direct conversation"));
        chat.People = [peer]; chat.Group = true;
        Assert.That(state.ConversationSubtitle(chat), Is.EqualTo("2 members · 1 active"));
        await state.ConnectivityChanged(false);
        Assert.That(state.IsActive(peer), Is.False, "Cached heartbeats must not show online when this device is offline");
    }

    private sealed class Handler(UserSession user, bool fail) : HttpMessageHandler
    {
        public int Heartbeats;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("presence")) { Heartbeats++; return Task.FromResult(new HttpResponseMessage(fail ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.NoContent)); }
            object value = path == "/api/session" ? new SessionResponse(user, "token")
                : path.EndsWith("initialize") ? new ChatDefaults(Guid.NewGuid(), [])
                : new ChatPage<Conversation>([], 0);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(value) });
        }
    }
}
