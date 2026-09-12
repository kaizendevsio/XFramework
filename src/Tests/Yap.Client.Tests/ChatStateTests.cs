using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Client.Tests;

public sealed class ChatStateTests
{
    [Test]
    public void DiagnosticLogger_DoesNotFormatOrRecordSensitiveErrorDetails()
    {
        var js = new Mock<IJSRuntime>();
        using var provider = new DiagnosticsLoggerProvider(js.Object);
        var formatted = false;
        provider.CreateLogger("private-category").Log(LogLevel.Error, new EventId(17), "private-message",
            new InvalidOperationException("private-password"), (state, exception) => { formatted = true; return state; });
        Assert.That(formatted, Is.False);
        var recorded = JsonSerializer.Serialize(js.Invocations.Single().Arguments[1]);
        Assert.That(recorded, Does.Contain("InvalidOperationException"));
        Assert.That(recorded, Does.Not.Contain("private-"));
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public async Task DeleteConversation_RemainsHiddenAfterRefresh_AndPreservesPendingSends(bool pending, bool everyone)
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Sender");
        var chat = new Conversation { Id = Guid.NewGuid() }; var deletes = 0;
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(true);
        using var http = new HttpClient(new Handler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/session") return Json(new SessionResponse(user, "token"));
            if (path.EndsWith("initialize")) return Json(new ChatDefaults(Guid.NewGuid(), []));
            if (path.EndsWith("thread-actions"))
            {
                var action = (await request.Content!.ReadFromJsonAsync<ThreadAction>())!;
                Assert.That(action.Action, Is.EqualTo(everyone ? "delete-for-everyone" : "delete-for-me"));
                deletes++; chat.Removed = true; return new(HttpStatusCode.NoContent);
            }
            return Json(new ChatPage<Conversation>([chat], 1));
        })) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync();
        if (pending)
        {
            var message = fixture.Message(chat.Id); var item = fixture.Queue(message);
            item.Scope = OfflineStore.Scope(user); item.Paused = true;
            await fixture.Store.QueueAsync(item, message, "main");
            Assert.ThrowsAsync<InvalidOperationException>(() => state.DeleteConversationAsync(chat.Id, everyone));
            Assert.That(deletes, Is.Zero);
            Assert.That(await fixture.Store.PendingAsync(item.Scope), Has.Count.EqualTo(1));
        }
        else
        {
            await state.DeleteConversationAsync(chat.Id, everyone); await state.SynchronizeAsync();
            Assert.That(deletes, Is.EqualTo(1)); Assert.That(state.Conversations, Is.Empty);
            Assert.That(state.HasMoreConversations, Is.False, "Archived entries must not leave an endless Load more button");
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Reconnect_OnlyConfirmedDeletionClearsCachedChatAndOutbox(bool confirmed)
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Recipient");
        var scope = OfflineStore.Scope(user); var chat = new Conversation { Id = Guid.NewGuid() };
        await fixture.Store.SetSettingAsync("user", JsonSerializer.Serialize(user));
        await fixture.Store.SaveConversationsAsync(scope, [chat]);
        var message = fixture.Message(chat.Id); var pending = fixture.Queue(message);
        pending.Scope = scope; pending.Paused = true;
        await fixture.Store.QueueAsync(pending, message, "main");
        await fixture.Store.SaveDraftAsync(scope, $"{chat.Id:N}:main", "draft");
        // Even the same conversation ID in another account's cache must be preserved.
        await fixture.Store.SaveMessagesAsync("other-account", [message]);
        var online = false; var removed = new List<Guid>(); var deletions = new List<Guid>();
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).Returns(() => ValueTask.FromResult(online));
        using var http = new HttpClient(new Handler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/session") return Task.FromResult(Json(new SessionResponse(user, "token")));
            if (path.EndsWith("initialize")) return Task.FromResult(Json(new ChatDefaults(Guid.NewGuid(), [])));
            if (path.EndsWith(chat.Id.ToString())) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Get), "Deleted or paused messages must never be sent.");
            return Task.FromResult(Json(new ChatPage<Conversation>([], 0)));
        }, deletions)) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync(); await state.SelectAsync(chat.Id);
        state.ConversationRemoved += removed.Add;
        if (confirmed) deletions.Add(chat.Id);
        online = true; await state.ConnectivityChanged(true);
        Assert.That(await fixture.Store.MessagesAsync(scope, chat.Id), Has.Count.EqualTo(confirmed ? 0 : 1));
        Assert.That(await fixture.Store.PendingAsync(scope), Has.Count.EqualTo(confirmed ? 0 : 1));
        Assert.That(await fixture.Store.DraftAsync(scope, $"{chat.Id:N}:main"), Is.EqualTo(confirmed ? "" : "draft"));
        Assert.That(removed, Has.Count.EqualTo(confirmed ? 1 : 0));
        Assert.That(state.Selected is null, Is.EqualTo(confirmed));
        Assert.That(await fixture.Store.MessagesAsync("other-account", chat.Id), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SendAsync_BackendBlip_LeavesTheMessageQueuedWithoutAlarmingTheReader()
    {
        // Bolt drops its socket periodically on the shared stack, so a send can meet a
        // 503 and succeed moments later. The queued badge on the bubble is the signal;
        // an error toast plus an "offline" claim is not.
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Sender");
        var thread = Guid.NewGuid();
        var chat = new Conversation { Id = thread };
        var failSend = true;
        var sends = 0;
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(true);
        using var http = new HttpClient(new Handler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/session") return Json(new SessionResponse(user, "token"));
            if (path.EndsWith("initialize")) return Json(new ChatDefaults(Guid.NewGuid(), []));
            if (path.EndsWith("/messages") && request.Method == HttpMethod.Post)
            {
                sends++;
                if (failSend) return new(HttpStatusCode.ServiceUnavailable);
                var sent = (await request.Content!.ReadFromJsonAsync<SendMessage>())!;
                return Json(new MessageReceipt(sent.Id));
            }
            if (path.EndsWith("/messages")) return Json(new ChatPage<ChatMessage>([], 0));
            if (path.EndsWith(thread.ToString())) return Json(chat);
            return Json(new ChatPage<Conversation>([chat], 1));
        })) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync();

        await state.SendAsync(thread, "hello", null, "main");
        await state.SynchronizeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(sends, Is.GreaterThan(0), "The send was attempted.");
            Assert.That(state.Error, Is.Null, "A retryable blip must not raise a notice.");
            Assert.That(state.Online, Is.True, "A backend fault is not the device being offline.");
            Assert.That(state.PendingCount, Is.EqualTo(1), "The message stays queued.");
        });

        failSend = false;
        await state.SynchronizeAsync();

        Assert.That(state.PendingCount, Is.EqualTo(0), "The retry clears the queue.");
    }

    [Test]
    public async Task RefreshHint_RendersBeforeReadReceiptAndKeepsEventsArrivingDuringRefresh()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Recipient");
        var friend = Guid.NewGuid(); var thread = Guid.NewGuid();
        var chat = new Conversation { Id = thread, People = [new(friend, "Sarah", "sarah")] };
        var messages = new List<ChatMessage>();
        var reading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sessionRequests = 0; var readRequests = 0;
        var typingRequests = new List<bool>();
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(true);
        using var http = new HttpClient(new Handler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/session") { sessionRequests++; return Json(new SessionResponse(user, "token")); }
            if (path.EndsWith("initialize")) return Json(new ChatDefaults(Guid.NewGuid(), []));
            if (path.EndsWith("thread-actions")) { typingRequests.Add((await request.Content!.ReadFromJsonAsync<ThreadAction>())!.Value); return new(HttpStatusCode.NoContent); }
            if (path.EndsWith("/read")) { readRequests++; reading.TrySetResult(); await release.Task; return new(HttpStatusCode.NoContent); }
            if (path.EndsWith("/messages")) return Json(new ChatPage<ChatMessage>(messages.ToList(), messages.Count));
            if (path.EndsWith(thread.ToString())) return Json(chat);
            return Json(new ChatPage<Conversation>([chat], 1));
        })) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync(); await state.SelectAsync(thread);
        messages.Add(new() { Id = Guid.NewGuid(), ThreadId = thread, SenderId = friend, Text = "First" });
        var refresh = state.RefreshHint();
        try
        {
            await reading.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(state.Selected!.Messages.Select(x => x.Text), Does.Contain("First"));
            messages.Add(new() { Id = Guid.NewGuid(), ThreadId = thread, SenderId = friend, Text = "Second" });
            _ = state.RefreshHint();
        }
        finally { release.TrySetResult(); }
        await refresh.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(state.Selected!.Messages.Select(x => x.Text), Does.Contain("Second"));
        await state.RefreshHint();
        Assert.That(readRequests, Is.EqualTo(2));
        Assert.That(sessionRequests, Is.EqualTo(1));
        state.TypingChanged(thread, user.CredentialId, true);
        state.TypingChanged(Guid.NewGuid(), friend, true);
        Assert.That(state.TypingText, Is.Null);
        state.TypingChanged(thread, friend, true);
        Assert.That(state.TypingText, Is.EqualTo("Sarah is typing…"));
        state.TypingChanged(thread, friend, false);
        Assert.That(state.TypingText, Is.Null);
        state.TypingChanged(thread, friend, true);
        await Task.Delay(6200);
        Assert.That(state.TypingText, Is.Null);
        await state.PublishTypingAsync(thread, true);
        await state.PublishTypingAsync(thread, true);
        await state.PublishTypingAsync(thread, false);
        await state.PublishTypingAsync(thread, true);
        Assert.That(typingRequests, Is.EqualTo(new[] { true, false, true }));
        await state.ConnectivityChanged(false);
        await state.PublishTypingAsync(thread, true);
        Assert.That(typingRequests, Has.Count.EqualTo(3));
    }

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
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle, List<Guid>? deleted = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            request.RequestUri!.AbsolutePath == "/api/chat/conversations/deleted"
                ? Task.FromResult(Json(new ChatPage<Guid>(deleted ?? [], deleted?.Count ?? 0))) : handle(request);
    }
}
