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

    [TestCase("Missed voice call", false, CallDirection.Missed, null, "Missed voice call")]
    [TestCase("Missed video call", true, CallDirection.Unanswered, null, "Unanswered video call")]
    [TestCase("Voice call · 5:09", false, CallDirection.Incoming, "5:09", "Incoming voice call")]
    [TestCase("Video call · 61:02", true, CallDirection.Outgoing, "61:02", "Outgoing video call")]
    public void Direction_IsCallerAndOutcome_AndDurationIsTheServersOwn(string text, bool mine, CallDirection direction, string? duration, string description)
    {
        var message = new ChatMessage { Text = text, IsCallSummary = true, Mine = mine };
        Assert.That(CallHistory.Direction(message), Is.EqualTo(direction));
        Assert.That(CallHistory.Duration(message), Is.EqualTo(duration));
        Assert.That(CallHistory.Describe(message), Is.EqualTo(description));
    }

    [TestCase("Voice call · 5:09", "5 minutes 9 seconds")]
    [TestCase("Voice call · 1:00", "1 minute")]
    [TestCase("Video call · 0:01", "1 second")]
    [TestCase("Video call · 0:00", "0 seconds")]
    [TestCase("Missed voice call", null)]
    public void SpokenDuration_ReadsMinutesAndSeconds(string text, string? spoken) =>
        Assert.That(CallHistory.SpokenDuration(new ChatMessage { Text = text, IsCallSummary = true }), Is.EqualTo(spoken));

    [Test]
    public void Recents_CollapseConsecutiveCallsWithOneConversation_WithinEachSection()
    {
        var alex = Guid.NewGuid(); var sam = Guid.NewGuid(); var now = new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
        CallHistoryItem Call(Guid thread, string text, int minutesAgo, bool mine = false) =>
            new(new() { Id = Guid.NewGuid(), ThreadId = thread, Text = text, IsCallSummary = true, Mine = mine, CreatedAt = now.AddMinutes(-minutesAgo) },
                thread == alex ? "Alex" : "Sam", false);
        var calls = new[]
        {
            Call(alex, "Missed voice call", 5), Call(alex, "Voice call · 5:09", 30, mine: true), Call(alex, "Missed voice call", 60),
            Call(sam, "Video call · 1:02", 90),
            Call(alex, "Voice call · 0:42", 120),
            // Yesterday: the same person again starts a new group under the new heading.
            Call(alex, "Missed video call", 24 * 60), Call(alex, "Voice call · 2:00", 25 * 60)
        };
        string Section(DateTime at) => at > now.AddHours(-12) ? "Today" : "Yesterday";

        // A later page can overlap and arrive out of order; the log is still newest first and unique.
        var sections = CallLog.Group([.. calls.Skip(3), .. calls.Take(4)], Section);

        Assert.That(sections.Select(s => s.Label), Is.EqualTo(new[] { "Today", "Yesterday" }));
        var today = sections[0].Groups;
        Assert.That(today.Select(g => (g.Latest.ConversationName, g.Calls.Count)), Is.EqualTo(new[] { ("Alex", 3), ("Sam", 1), ("Alex", 1) }));
        Assert.That(today[0].Calls.Select(c => c.Message.Id), Is.EqualTo(calls.Take(3).Select(c => c.Message.Id)), "Newest call leads its group.");
        Assert.That(today[0].Missed, Is.True, "The row takes the latest call's outcome.");
        Assert.That(today[0].Key, Is.EqualTo(calls[2].Message.Id), "The oldest call anchors the row so a new call does not collapse it.");
        Assert.That(today[1].Video, Is.True);
        Assert.That(today[1].Missed, Is.False);
        var yesterday = sections[1].Groups.Single();
        Assert.That(yesterday.Calls, Has.Count.EqualTo(2));
        Assert.That(yesterday.Direction, Is.EqualTo(CallDirection.Missed));
        Assert.That(CallLog.Group([], Section), Is.Empty);
    }

    [Test]
    public void Recents_KeyStaysPut_WhenANewerCallJoinsTheTopGroup()
    {
        var thread = Guid.NewGuid(); var now = DateTime.UtcNow;
        var older = new CallHistoryItem(new() { Id = Guid.NewGuid(), ThreadId = thread, Text = "Voice call · 1:00", IsCallSummary = true, CreatedAt = now.AddMinutes(-10) }, "Alex", false);
        var newer = older with { Message = new() { Id = Guid.NewGuid(), ThreadId = thread, Text = "Missed voice call", IsCallSummary = true, CreatedAt = now } };
        var before = CallLog.Group([older], _ => "Today").Single().Groups.Single();
        var after = CallLog.Group([newer, older], _ => "Today").Single().Groups.Single();
        Assert.That(after.Key, Is.EqualTo(before.Key));
        Assert.That(after.Calls, Has.Count.EqualTo(2));
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
