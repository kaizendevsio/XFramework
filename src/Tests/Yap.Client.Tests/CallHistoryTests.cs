using System.Net;
using System.Text.Json;
using Microsoft.JSInterop;
using Moq;
using NUnit.Framework;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Client.Tests;

public sealed class CallHistoryTests
{
    [TestCase("Video call · 1:24", true, true, "Video call · 1:24")]
    [TestCase("Missed video call", true, true, "Unanswered video call")]
    [TestCase("Missed voice call", false, false, "Missed voice call")]
    public void Cards_KeepCallTypeAndDistinguishUnansweredFromMissed(string text, bool mine, bool video, string label)
    {
        var message = new ChatMessage { Text = text, IsCallSummary = true, Mine = mine };
        Assert.That(CallHistory.IsVideo(message), Is.EqualTo(video));
        Assert.That(CallHistory.Label(message), Is.EqualTo(label));
        message.IsCallSummary = false;
        Assert.That(CallHistory.IsVideo(message), Is.False, "Ordinary messages cannot impersonate calls.");
    }

    [Test]
    public async Task CachedCalls_AreAccountBoundAndRemovedWithConversationOrLogout()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var store = fixture.Store;
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Sam");
        var scope = OfflineStore.Scope(user); var thread = Guid.NewGuid();
        var key = $"calls:{scope}:0";
        await store.SetSettingAsync("user", JsonSerializer.Serialize(user));
        var record = new CallHistoryItem(new() { Id = Guid.NewGuid(), ThreadId = thread, Text = "Missed video call", IsCallSummary = true }, "Alex", false);
        await store.SetSettingAsync(key, JsonSerializer.Serialize(new ChatPage<CallHistoryItem>([record], 1)));
        await store.SetSettingAsync("calls:another-account:0", "other");
        using var http = new HttpClient(new RejectNetwork()) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(store, new ChatApi(http), new Mock<IJSRuntime>().Object);
        await state.InitializeAsync();
        Assert.That((await state.CallsAsync()).Items.Single().Message.Id, Is.EqualTo(record.Message.Id));
        await store.RemoveConversationAsync(scope, thread);
        Assert.That((await state.CallsAsync()).Items, Is.Empty);
        Assert.That(await store.SettingAsync("calls:another-account:0"), Is.EqualTo("other"));
        await store.ClearPrivateAsync();
        Assert.That(await store.SettingAsync("calls:another-account:0"), Is.Null);
    }

    [Test]
    public async Task Startup_ShowsReadyInboxWhileCachedConversationsAreStillReading()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var store = fixture.Store; var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Sam");
        await store.SetSettingAsync("user", JsonSerializer.Serialize(user));
        await store.SaveConversationsAsync(OfflineStore.Scope(user), [new() { Id = Guid.NewGuid(), Name = "Cached chat" }]);
        using var http = new HttpClient(new RejectNetwork()) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(store, new ChatApi(http), new Mock<IJSRuntime>().Object);
        var release = new TaskCompletionSource(); Task? held = null;
        state.Changed += () => { if (state.Ready && held is null) held = store.UseAsync(async _ => { await release.Task; return true; }); };
        try
        {
            await state.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(2));
            Assert.That(state.Ready, Is.True);
            Assert.That(state.LoadingConversations, Is.True);
            Assert.That(state.Conversations, Is.Empty);
        }
        finally { release.TrySetResult(); }
        for (var n = 0; n < 100 && state.LoadingConversations; n++) await Task.Delay(10);
        Assert.That(state.Conversations.Single().Name, Is.EqualTo("Cached chat"));
    }

    private sealed class RejectNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new AssertionException("Offline cache and startup must not wait for HTTP.");
    }
}
