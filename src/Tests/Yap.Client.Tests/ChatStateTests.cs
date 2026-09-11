using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using Moq;
using NUnit.Framework;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Client.Tests;

public sealed class ChatStateTests
{
    [Test]
    public async Task SynchronizeAsync_ResponseLostAfterCommit_RetriesSameIdAndClearsQueueAfterReceipt()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Test account");
        await fixture.Store.SetSettingAsync("user", JsonSerializer.Serialize(user));
        var online = false;
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).Returns(() => ValueTask.FromResult(online));
        var ids = new List<Guid>();
        var committed = new HashSet<Guid>();
        using var http = new HttpClient(new Handler(async request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/session") return Json(new SessionResponse(user, "token"));
            Assert.That(request.Headers.GetValues("X-Yap-Account").Single(), Is.EqualTo(OfflineStore.Scope(user)));
            if (request.RequestUri.AbsolutePath.EndsWith("initialize")) return Json(new ChatDefaults(Guid.NewGuid(), []));
            if (request.RequestUri.AbsolutePath.EndsWith("messages"))
            {
                var message = await request.Content!.ReadFromJsonAsync<SendMessage>();
                ids.Add(message!.Id); committed.Add(message.Id);
                if (ids.Count == 1) throw new HttpRequestException("Response lost after server commit");
                return Json(new MessageReceipt(message.Id));
            }
            return Json(new ChatPage<Conversation>([], 0));
        })) { BaseAddress = new Uri("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync();
        var thread = Guid.NewGuid();
        await state.SendAsync(thread, "offline message", null, "main");
        online = true;
        await state.RetryAsync();
        Assert.That(await fixture.Store.PendingAsync(OfflineStore.Scope(user)), Has.Count.EqualTo(1));
        await state.RetryAsync();
        Assert.That(ids, Has.Count.EqualTo(2));
        Assert.That(ids.Distinct().Count(), Is.EqualTo(1));
        Assert.That(committed, Has.Count.EqualTo(1));
        Assert.That(await fixture.Store.PendingAsync(OfflineStore.Scope(user)), Is.Empty);
        Assert.That((await fixture.Store.MessagesAsync(OfflineStore.Scope(user), thread)).Single().Delivery, Is.EqualTo("Sent"));
    }

    [Test]
    public async Task SynchronizeAsync_CookieChanged_DoesNotSendOldAccountsOutbox()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var cached = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Original account");
        var switched = cached with { CredentialId = Guid.NewGuid(), Name = "Other account" };
        await fixture.Store.SetSettingAsync("user", JsonSerializer.Serialize(cached));
        var online = false;
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).Returns(() => ValueTask.FromResult(online));
        var requests = new List<string>();
        using var http = new HttpClient(new Handler(request =>
        { requests.Add(request.RequestUri!.AbsolutePath); return Task.FromResult(Json(new SessionResponse(switched, "token"))); }))
        { BaseAddress = new Uri("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync();
        await state.SendAsync(Guid.NewGuid(), "private queued message", null, "main");
        online = true; await state.RetryAsync();
        Assert.That(state.NeedsLogin, Is.True);
        Assert.That(requests, Is.EqualTo(new[] { "/api/session" }));
        Assert.That(await fixture.Store.PendingAsync(OfflineStore.Scope(cached)), Has.Count.EqualTo(1));
        Assert.That(await fixture.Store.PendingAsync(OfflineStore.Scope(switched)), Is.Empty);
    }

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => handle(request);
    }
}
