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
    public async Task RefreshHint_ReadsInParallel_AndLetsQueuedSendRunBeforeInbox()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Sender");
        var thread = Guid.NewGuid(); var chat = new Conversation { Id = thread };
        var block = false; var order = new List<string>();
        var detailsStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var messagesStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var posted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(true);
        using var http = new HttpClient(new Handler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/session") return Json(new SessionResponse(user, "token"));
            if (path.EndsWith("initialize")) return Json(new ChatDefaults(Guid.NewGuid(), []));
            if (request.Method == HttpMethod.Post && path.EndsWith("messages"))
            {
                var send = (await request.Content!.ReadFromJsonAsync<SendMessage>())!;
                order.Add("send"); posted.TrySetResult(); return Json(new MessageReceipt(send.Id));
            }
            if (path.EndsWith(thread.ToString()))
            {
                if (block) { detailsStarted.TrySetResult(); await release.Task; }
                return Json(chat);
            }
            if (path.EndsWith("messages"))
            {
                if (block) messagesStarted.TrySetResult();
                return Json(new ChatPage<ChatMessage>([], 0));
            }
            if (block) order.Add("inbox");
            return Json(new ChatPage<Conversation>([chat], 1));
        })) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync(); await state.SelectAsync(thread);
        block = true;
        var refresh = state.RefreshHint();
        try
        {
            await detailsStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await messagesStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await state.SendAsync(thread, "Send while refreshing", null, "main");
            for (var i = 0; i < 100 && (await fixture.Store.PendingAsync(OfflineStore.Scope(user))).Count == 0; i++) await Task.Delay(10);
            await Task.Delay(50); // Let the persisted send join the held network gate.
        }
        finally { release.TrySetResult(); }
        await posted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await refresh.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.That(order.First(), Is.EqualTo("send"), "Inbox work must yield to an already queued send.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SendWithLiveSession_PostsDirectly_AndRevalidatesRejectedSession(bool reject)
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Sender");
        var scope = OfflineStore.Scope(user);
        await fixture.Store.SetSettingAsync("user", JsonSerializer.Serialize(user));
        var online = false; var sessions = 0; var posted = new TaskCompletionSource();
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).Returns(() => ValueTask.FromResult(online));
        using var http = new HttpClient(new Handler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/session") return Task.FromResult(Json(new SessionResponse(++sessions > 1 && reject ? null : user, "token")));
            if (path.EndsWith("initialize")) return Task.FromResult(Json(new ChatDefaults(Guid.NewGuid(), [])));
            if (path.EndsWith("messages"))
            {
                Assert.That(request.Headers.GetValues("X-Yap-Account"), Is.EqualTo(new[] { scope }));
                Assert.That(request.Headers.GetValues("RequestVerificationToken"), Is.EqualTo(new[] { "token" }));
                posted.TrySetResult();
                return Task.FromResult(new HttpResponseMessage(reject ? HttpStatusCode.Unauthorized : HttpStatusCode.ServiceUnavailable));
            }
            return Task.FromResult(Json(new ChatPage<Conversation>([], 0)));
        })) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync(); online = true;
        await state.SynchronizeAsync();
        await state.SendAsync(Guid.NewGuid(), "Keep this queued on rejection", null, "main");
        await posted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        for (var i = 0; i < 100 && !state.NeedsLogin && state.Online; i++) await Task.Delay(10);
        Assert.That(sessions, Is.EqualTo(reject ? 2 : 1), "Only a rejected session needs another probe; ordinary sending bypasses full sync.");
        Assert.That(state.NeedsLogin, Is.EqualTo(reject));
        Assert.That(await fixture.Store.PendingAsync(scope), Has.Count.EqualTo(1));
        if (!reject) Assert.That(state.Error, Is.Null, "Transient delivery failure is quiet and remains queued.");
    }

    [Test]
    public async Task SendBurst_QueuesWithoutWaitingForNetwork_AndDrainsNewMessagesInTheSameSync()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Sender");
        var scope = OfflineStore.Scope(user); var thread = Guid.NewGuid();
        await fixture.Store.SetSettingAsync("user", JsonSerializer.Serialize(user));
        var online = false; var sessions = 0;
        var firstPost = new TaskCompletionSource(); var release = new TaskCompletionSource();
        var posted = new List<string>(); var ids = new HashSet<Guid>();
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).Returns(() => ValueTask.FromResult(online));
        using var http = new HttpClient(new Handler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/session") { sessions++; return Json(new SessionResponse(user, "token")); }
            if (path.EndsWith("initialize")) return Json(new ChatDefaults(Guid.NewGuid(), []));
            if (path.EndsWith("messages"))
            {
                var message = (await request.Content!.ReadFromJsonAsync<SendMessage>())!;
                if (posted.Count == 0) { firstPost.TrySetResult(); await release.Task; }
                posted.Add(message.Text); ids.Add(message.Id);
                return Json(new MessageReceipt(message.Id));
            }
            return Json(new ChatPage<Conversation>([], 0));
        })) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync(); online = true;
        await state.SendAsync(thread, "Message 0", null, "main"); await firstPost.Task;
        try
        {
            for (var i = 1; i < 12; i++) await state.SendAsync(thread, $"Message {i}", null, "main").WaitAsync(TimeSpan.FromSeconds(1));
            Assert.That(await fixture.Store.PendingAsync(scope), Has.Count.EqualTo(12));
            Assert.That(release.Task.IsCompleted, Is.False, "All local sends complete while the first network send is still stalled.");
            Assert.That(sessions, Is.EqualTo(1));
        }
        finally { release.TrySetResult(); }
        await state.SynchronizeAsync();
        Assert.That(posted, Is.EqualTo(Enumerable.Range(0, 12).Select(i => $"Message {i}")));
        Assert.That(ids, Has.Count.EqualTo(12));
        Assert.That(sessions, Is.EqualTo(2), "One burst sync plus the explicit verification sync, not one full sync per send.");
        Assert.That(await fixture.Store.PendingAsync(scope), Is.Empty);
    }

    [Test]
    public async Task QueueingPreviousMessage_DoesNotEraseNewDraft()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Sender");
        var scope = OfflineStore.Scope(user);
        await fixture.Store.SetSettingAsync("user", JsonSerializer.Serialize(user));
        await fixture.Store.SaveDraftAsync(scope, "main", "Next message already being typed");
        using var http = new HttpClient(new Handler(_ => throw new AssertionException("Offline"))) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), Mock.Of<IJSRuntime>());
        await state.InitializeAsync();
        await state.SendAsync(Guid.NewGuid(), "Previous message", null, "main");
        Assert.That(await fixture.Store.DraftAsync(scope, "main"), Is.EqualTo("Next message already being typed"));
    }

    [Test]
    public async Task OpenConversation_ShowsCachedMessagesBeforeSlowBackgroundSyncFinishes()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Reader");
        var scope = OfflineStore.Scope(user);
        var chat = new Conversation { Id = Guid.NewGuid(), Name = "Saved conversation" };
        var message = new ChatMessage { Id = Guid.NewGuid(), ThreadId = chat.Id, Text = "Saved message" };
        await fixture.Store.SetSettingAsync("user", JsonSerializer.Serialize(user));
        await fixture.Store.SaveConversationsAsync(scope, [chat]);
        await fixture.Store.SaveMessagesAsync(scope, [message]);
        var started = new TaskCompletionSource(); var release = new TaskCompletionSource();
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(true);
        using var http = new HttpClient(new Handler(async _ => { started.TrySetResult(); await release.Task; throw new HttpRequestException("Connection lost"); })) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync(); await started.Task;
        var displayed = new TaskCompletionSource();
        state.Changed += () => { if (state.Selected?.Id == chat.Id && state.Selected.Messages.Count > 0) displayed.TrySetResult(); };
        var selection = state.SelectAsync(chat.Id);
        try
        {
            await displayed.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.That(release.Task.IsCompleted, Is.False);
            Assert.That(state.Selected!.Name, Is.EqualTo(chat.Name));
            Assert.That(state.Selected.Messages.Single().Text, Is.EqualTo(message.Text));
        }
        finally { release.TrySetResult(); await selection; }
    }

    [TestCase("transport")]
    [TestCase("timeout")]
    [TestCase("unavailable")]
    public async Task OfflineRetries_StayQuietDespiteBrowserOnlineHints_AndReconnectSendsQueue(string failure)
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Reader");
        var scope = OfflineStore.Scope(user); var thread = Guid.NewGuid();
        await fixture.Store.SetSettingAsync("user", JsonSerializer.Serialize(user));
        var browserOnline = false; var reachable = false; var sends = 0;
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).Returns(() => ValueTask.FromResult(browserOnline));
        using var http = new HttpClient(new Handler(async request =>
        {
            if (!reachable)
            {
                if (failure == "transport") throw new HttpRequestException("Network unavailable");
                if (failure == "timeout") throw new TaskCanceledException("Timed out");
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/session") return Json(new SessionResponse(user, "token"));
            if (path.EndsWith("initialize")) return Json(new ChatDefaults(Guid.NewGuid(), []));
            if (path.EndsWith("messages"))
            {
                sends++;
                return Json(new MessageReceipt((await request.Content!.ReadFromJsonAsync<SendMessage>())!.Id));
            }
            return Json(new ChatPage<Conversation>([], 0));
        })) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync();
        await state.SendAsync(thread, "Queued while offline", null, "main");
        await fixture.Store.SaveDraftAsync(scope, "main", "Still writing");
        var observed = new List<(bool Online, string? Error)>();
        state.Changed += () => observed.Add((state.Online, state.Error));
        browserOnline = true;
        for (var i = 0; i < 3; i++)
        {
            await state.ConnectivityChanged(true);
            await state.SynchronizeAsync();
            state.Report(new HttpRequestException("Attachment fetch failed"));
        }
        Assert.That(observed, Has.All.EqualTo((false, (string?)null)), "No false reconnects or repeated error notifications.");
        Assert.That(state.User, Is.EqualTo(user));
        Assert.That(state.NeedsLogin, Is.False);
        Assert.That(await fixture.Store.DraftAsync(scope, "main"), Is.EqualTo("Still writing"));
        Assert.That(await fixture.Store.PendingAsync(scope), Has.Count.EqualTo(1));
        reachable = true;
        await state.ConnectivityChanged(true);
        await state.SynchronizeAsync();
        Assert.That(state.Online, Is.True);
        Assert.That(state.Error, Is.Null);
        Assert.That(sends, Is.EqualTo(1));
        Assert.That(await fixture.Store.PendingAsync(scope), Is.Empty);
        state.Report(new InvalidOperationException("Storage failed"));
        Assert.That(state.Error, Is.Not.Null, "Real failures must remain visible.");
    }

    [Test]
    public async Task OfflineEncryptedAttachments_UseVerifiedCacheWithoutRequestingSenderKeys()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Reader");
        await fixture.Store.SetSettingAsync("user", JsonSerializer.Serialize(user));
        var cached = true;
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.hasVerifiedFile", It.IsAny<object?[]?>())).Returns(() => ValueTask.FromResult(cached));
        using var http = new HttpClient(new Handler(_ => throw new AssertionException("Offline attachments must not make requests"))) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync();
        var message = new ChatMessage { Id = Guid.NewGuid(), ThreadId = Guid.NewGuid(), SenderId = user.CredentialId, EncryptedEnvelope = "encrypted" };
        var file = new ChatAttachment(Guid.NewGuid(), "photo.jpg", "image/jpeg", 100);
        Assert.That(await state.PrepareEncryptedFileAsync(message, file), Does.EndWith(".verified"));
        cached = false;
        Assert.ThrowsAsync<HttpRequestException>(() => state.PrepareEncryptedFileAsync(message, file));
        Assert.That(state.Error, Is.Null);
    }

    [Test]
    public async Task InboxPreview_DecryptsWithoutOpeningHistory_ReusesCacheAndRefreshesEditedCiphertext()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Reader");
        var thread = Guid.NewGuid(); var id = Guid.NewGuid(); var envelope = "first-ciphertext";
        var online = false;
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).Returns(() => ValueTask.FromResult(online));
        js.Setup(x => x.InvokeAsync<ChatEncryption.EncryptedMessageContent>("yap.encryption.decrypt", It.IsAny<object?[]?>()))
            .Returns(() => ValueTask.FromResult(new ChatEncryption.EncryptedMessageContent(envelope == "first-ciphertext" ? "Hello" : "Edited", [])));
        using var http = new HttpClient(new Handler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            Assert.That(path.EndsWith("/messages"), Is.False, "Inbox must not download message history.");
            if (path == "/api/session") return Task.FromResult(Json(new SessionResponse(user, "token")));
            if (path.EndsWith("initialize")) return Task.FromResult(Json(new ChatDefaults(Guid.NewGuid(), [])));
            if (path.Contains("/encryption/people/")) return Task.FromResult(Json(new { credentialId = user.CredentialId }));
            return Task.FromResult(Json(new ChatPage<Conversation>([new() { Id = thread, Preview = "Encrypted message",
                LastMessage = new() { Id = id, ThreadId = thread, SenderId = user.CredentialId, EncryptedEnvelope = envelope } }], 1)));
        })) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync(); online = true;
        await state.SynchronizeAsync();
        Assert.That(state.Conversations.Single().Preview, Is.EqualTo("Hello"));
        await state.SynchronizeAsync();
        js.Verify(x => x.InvokeAsync<ChatEncryption.EncryptedMessageContent>("yap.encryption.decrypt", It.IsAny<object?[]?>()), Times.Once);
        envelope = "edited-ciphertext";
        await state.SynchronizeAsync();
        Assert.That(state.Conversations.Single().Preview, Is.EqualTo("Edited"));
        Assert.That((await fixture.Store.ConversationsAsync(OfflineStore.Scope(user))).Single().Preview, Is.EqualTo("Edited"));
        js.Verify(x => x.InvokeAsync<ChatEncryption.EncryptedMessageContent>("yap.encryption.decrypt", It.IsAny<object?[]?>()), Times.Exactly(2));
    }

    [Test]
    public async Task ReplyHistory_StaysBoundedAndKeepsItsAnchorAcrossRefresh()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Reader");
        var chat = new Conversation { Id = Guid.NewGuid() };
        var root = new ChatMessage { Id = Guid.NewGuid(), ThreadId = chat.Id, Text = "Root", CreatedAt = DateTime.UtcNow.Date, ReplyTotal = 250 };
        var messages = Enumerable.Range(1, 250).Select(i => new ChatMessage { Id = Guid.NewGuid(), ThreadId = chat.Id, ParentId = root.Id, IsThreadReply = true, Text = $"Reply {i}", CreatedAt = root.CreatedAt.AddSeconds(i) }).ToList();
        messages.Insert(0, root);
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(true);
        using var http = new HttpClient(new Handler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/session") return Task.FromResult(Json(new SessionResponse(user, "token")));
            if (path.EndsWith("initialize")) return Task.FromResult(Json(new ChatDefaults(Guid.NewGuid(), [])));
            if (path.EndsWith("/messages"))
            {
                var reply = request.RequestUri.Query.Contains("parent=");
                var page = int.Parse(request.RequestUri.Query.Split("page=")[1]);
                var all = messages.Where(x => !reply || x.ParentId == root.Id).OrderByDescending(x => x.CreatedAt).ToList();
                return Task.FromResult(Json(new ChatPage<ChatMessage>(all.Skip(page * 50).Take(50).ToList(), all.Count)));
            }
            if (path.EndsWith(chat.Id.ToString())) return Task.FromResult(Json(chat));
            return Task.FromResult(Json(new ChatPage<Conversation>([chat], 1)));
        })) { BaseAddress = new("https://yap.test/") };
        await fixture.Store.SaveMessagesAsync(OfflineStore.Scope(user), messages);
        await using var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync(); await state.SelectAsync(chat.Id); await state.LocateAsync(root.Id);
        var parent = state.Selected!.Messages.Single(x => x.Id == root.Id);
        await state.LoadRepliesAsync(parent); await state.LoadRepliesAsync(parent);
        Assert.That(parent.Replies, Has.Count.EqualTo(100));
        var anchor = parent.Replies[^1].Id;
        messages.Add(new() { Id = Guid.NewGuid(), ThreadId = chat.Id, ParentId = root.Id, IsThreadReply = true, Text = "New reply", CreatedAt = root.CreatedAt.AddSeconds(251) });
        await state.RefreshHint();
        parent = state.Selected!.Messages.Single(x => x.Id == root.Id);
        Assert.That(parent.Replies, Has.Count.EqualTo(100));
        Assert.That(parent.Replies[^1].Id, Is.EqualTo(anchor));
        Assert.That(state.HasNewerReplies, Is.True);
    }

    [Test]
    public async Task HistoryPaging_IsBoundedInBothDirections_AndReleasesMessagesOnLeave()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Reader");
        var scope = OfflineStore.Scope(user); var chat = new Conversation { Id = Guid.NewGuid(), MessageTotal = 600 };
        await fixture.Store.SetSettingAsync("user", JsonSerializer.Serialize(user));
        await fixture.Store.SaveConversationsAsync(scope, [chat]);
        await fixture.Store.SaveMessagesAsync(scope, Enumerable.Range(0, 600).Select(i => new ChatMessage { Id = Guid.NewGuid(), ThreadId = chat.Id, Text = $"History {i}", CreatedAt = DateTime.UtcNow.Date.AddSeconds(i) }));
        using var http = new HttpClient(new Handler(_ => throw new InvalidOperationException("Offline history must not use the server"))) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), Mock.Of<IJSRuntime>());
        await state.InitializeAsync(); await state.SelectAsync(chat.Id);
        for (var i = 0; state.HasEarlierMessages && i < 20; i++)
        { await state.LoadEarlierAsync(); Assert.That(state.Selected!.Messages.Count, Is.LessThanOrEqualTo(100)); }
        Assert.That(state.Selected!.Messages[0].Text, Is.EqualTo("History 0"));
        Assert.That(state.HasEarlierMessages, Is.False);
        for (var i = 0; state.HasNewerMessages && i < 20; i++)
        { await state.LoadNewerAsync(); Assert.That(state.Selected!.Messages.Count, Is.LessThanOrEqualTo(100)); }
        Assert.That(state.Selected!.Messages[^1].Text, Is.EqualTo("History 599"));
        var newest = state.Selected.Messages[^1].Id;
        for (var i = 0; i < 5; i++) await state.LoadEarlierAsync();
        await state.LocateAsync(newest);
        Assert.That(state.Selected.Messages.Any(x => x.Id == newest), Is.True, "Search must reach newer cached messages from an older window.");
        Assert.That(state.Selected.Messages.Count, Is.LessThanOrEqualTo(100));
        var retained = state.Selected;
        state.LeaveConversation(chat.Id);
        Assert.That(state.Selected, Is.Null);
        Assert.That(retained.Messages, Is.Empty);
        Assert.That(await fixture.Store.MessageCountAsync(scope, chat.Id), Is.EqualTo(600));
    }

    [Test]
    public async Task DuplicateReactionTap_OnlySendsOneAction_AndReconcilesConflict()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Sender");
        var chat = new Conversation { Id = Guid.NewGuid() };
        var message = new ChatMessage { Id = Guid.NewGuid(), ThreadId = chat.Id, Text = "hello" };
        var started = new TaskCompletionSource(); var resume = new TaskCompletionSource(); var actions = 0;
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(true);
        using var http = new HttpClient(new Handler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/session") return Json(new SessionResponse(user, "token"));
            if (path.EndsWith("initialize")) return Json(new ChatDefaults(Guid.NewGuid(), []));
            if (path.EndsWith("message-actions")) { actions++; started.SetResult(); await resume.Task; return new(HttpStatusCode.Conflict); }
            return Json(new ChatPage<Conversation>([chat], 1));
        })) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync();
        var first = state.ActionAsync(message, "react", emoji: "heart"); await started.Task;
        await state.ActionAsync(message, "react", emoji: "heart"); resume.SetResult(); await first;
        Assert.That(actions, Is.EqualTo(1));
        Assert.That(state.Error, Is.Null);
    }

    [Test]
    public async Task PreviouslyPausedAttachmentConflict_ResumesWithoutResendingMessage()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Sender");
        var scope = OfflineStore.Scope(user); var chat = new Conversation { Id = Guid.NewGuid() };
        var message = new ChatMessage { Id = Guid.NewGuid(), ThreadId = chat.Id, Text = "photo" };
        await fixture.Store.QueueAsync(new QueuedMessage { Scope = scope, Id = message.Id, ThreadId = chat.Id, Text = message.Text,
            MessageConfirmed = true, StorageId = Guid.NewGuid(), Paused = true,
            Error = "This change conflicts with a message already saved. Review it before retrying." }, message, "main");
        var attachments = 0; var sends = 0;
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(true);
        using var http = new HttpClient(new Handler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/session") return Task.FromResult(Json(new SessionResponse(user, "token")));
            if (path.EndsWith("initialize")) return Task.FromResult(Json(new ChatDefaults(Guid.NewGuid(), [])));
            if (path.EndsWith("/attachments")) { attachments++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)); }
            if (path.EndsWith("/messages")) sends++;
            return Task.FromResult(Json(new ChatPage<Conversation>([chat], 1)));
        })) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync();
        Assert.That(attachments, Is.EqualTo(1)); Assert.That(sends, Is.Zero);
        Assert.That(await fixture.Store.PendingAsync(scope), Is.Empty);
    }

    [Test]
    public async Task ReadReceipts_RequireVisibleMessages_AndLeavingStopsLateRefresh()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Recipient");
        var chat = new Conversation { Id = Guid.NewGuid() };
        var messages = new List<ChatMessage> { new() { Id = Guid.NewGuid(), ThreadId = chat.Id, SenderId = Guid.NewGuid(), Text = "First", CreatedAt = DateTime.UtcNow }, new() { Id = Guid.NewGuid(), ThreadId = chat.Id, SenderId = Guid.NewGuid(), Text = "Second", CreatedAt = DateTime.UtcNow.AddSeconds(1) } };
        Guid[] visible = []; var receipts = new List<Guid>();
        TaskCompletionSource? started = null, resume = null;
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(true);
        js.Setup(x => x.InvokeAsync<Guid[]>("yap.device.visibleMessages", It.IsAny<object?[]?>())).Returns(() => ValueTask.FromResult(visible));
        using var http = new HttpClient(new Handler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/session") return Json(new SessionResponse(user, "token"));
            if (path.EndsWith("initialize")) return Json(new ChatDefaults(Guid.NewGuid(), []));
            if (path.EndsWith("/read")) { receipts.AddRange((await request.Content!.ReadFromJsonAsync<ReadMessages>())!.MessageIds); return new(HttpStatusCode.NoContent); }
            if (path.EndsWith("/messages")) { if (resume is not null) { started!.SetResult(); await resume.Task; } return Json(new ChatPage<ChatMessage>(messages, messages.Count)); }
            if (path.EndsWith(chat.Id.ToString())) return Json(chat);
            return Json(new ChatPage<Conversation>([chat], 1));
        })) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync(); await state.SelectAsync(chat.Id);
        await state.ReadVisibleAsync(chat.Id); Assert.That(receipts, Is.Empty);
        visible = [messages[0].Id]; await state.ReadVisibleAsync(chat.Id); await state.ReadVisibleAsync(chat.Id);
        Assert.That(receipts, Is.EqualTo(visible));
        started = new(); resume = new();
        var refresh = state.RefreshHint(); await started.Task;
        state.LeaveConversation(chat.Id); visible = [messages[1].Id]; resume.SetResult();
        await refresh; await state.ReadVisibleAsync(chat.Id);
        Assert.That(state.Selected, Is.Null, "A late response cannot reopen a conversation after navigation");
        Assert.That(receipts, Is.EqualTo(new[] { messages[0].Id }));
    }

    [Test]
    public async Task RuntimeErrorAndTemporaryServiceFailure_KeepSavedSignInAndDrafts()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Recipient");
        await fixture.Store.SetSettingAsync("user", JsonSerializer.Serialize(user));
        await fixture.Store.SaveDraftAsync(OfflineStore.Scope(user), "main", "Keep this draft");
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(true);
        using var http = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)))) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync(); state.Report(new InvalidOperationException("Runtime failure"));
        await state.SynchronizeAsync();
        Assert.That(state.User, Is.EqualTo(user)); Assert.That(state.NeedsLogin, Is.False);
        Assert.That(await fixture.Store.SettingAsync("pendingLogout"), Is.Not.EqualTo("true"));
        Assert.That(await fixture.Store.DraftAsync(OfflineStore.Scope(user), "main"), Is.EqualTo("Keep this draft"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task UpstreamUnauthorized_OnlyRequiresSignInWhenSessionAlsoEnded(bool ended)
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Recipient");
        var sessions = 0;
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(true);
        using var http = new HttpClient(new Handler(request => Task.FromResult(request.RequestUri!.AbsolutePath == "/api/session"
            ? Json(new SessionResponse(++sessions > 1 && ended ? null : user, "token"))
            : new HttpResponseMessage(HttpStatusCode.Unauthorized)))) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync();
        Assert.That(state.User, Is.EqualTo(user));
        Assert.That(state.NeedsLogin, Is.EqualTo(ended));
        Assert.That(sessions, Is.EqualTo(2));
    }

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
        js.Setup(x => x.InvokeAsync<Guid[]>("yap.device.visibleMessages", It.IsAny<object?[]?>())).Returns(() => ValueTask.FromResult(messages.Select(x => x.Id).ToArray()));
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
        await state.RefreshHint();
        var readingVisible = state.ReadVisibleAsync(thread);
        try
        {
            await reading.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(state.Selected!.Messages.Select(x => x.Text), Does.Contain("First"));
            messages.Add(new() { Id = Guid.NewGuid(), ThreadId = thread, SenderId = friend, Text = "Second" });
            await state.RefreshHint().WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { release.TrySetResult(); }
        await readingVisible.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(state.Selected!.Messages.Select(x => x.Text), Does.Contain("Second"));
        await state.RefreshHint();
        await state.ReadVisibleAsync(thread);
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

    [Test]
    public async Task SelectAsync_ThreadReplies_StayInTheirThread_AndOnlyInlineRepliesAreQuoted()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Sender");
        var scope = OfflineStore.Scope(user);
        var chat = new Conversation { Id = Guid.NewGuid() };
        await fixture.Store.SetSettingAsync("user", JsonSerializer.Serialize(user));
        await fixture.Store.SaveConversationsAsync(scope, [chat]);
        var now = DateTime.UtcNow.Date;
        var root = new ChatMessage { Id = Guid.NewGuid(), ThreadId = chat.Id, Text = "root", CreatedAt = now };
        var inline = new ChatMessage { Id = Guid.NewGuid(), ThreadId = chat.Id, Text = "inline", CreatedAt = now.AddMinutes(1), ParentId = root.Id };
        var reply = new ChatMessage { Id = Guid.NewGuid(), ThreadId = chat.Id, Text = "in thread", CreatedAt = now.AddMinutes(2), ParentId = root.Id, IsThreadReply = true };
        await fixture.Store.SaveMessagesAsync(scope, [root, inline, reply]);
        using var http = new HttpClient(new Handler(_ => throw new AssertionException("Offline selection must not use HTTP")))
        { BaseAddress = new Uri("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), new Mock<IJSRuntime>().Object);
        await state.InitializeAsync();
        await state.SelectAsync(chat.Id);
        var messages = state.Selected!.Messages;
        Assert.That(messages.Select(x => x.Id), Is.EqualTo(new[] { root.Id, inline.Id }));
        Assert.That(messages.Single(x => x.Id == inline.Id).Quote!.Id, Is.EqualTo(root.Id));
        Assert.That(messages.Single(x => x.Id == root.Id).Replies, Is.Empty, "The timeline never carries a thread's replies.");
    }

    // 15 = the tenant default, 0 = editing disabled, admin = the server-side window bypass.
    [TestCase(15, false, 5, true)]
    [TestCase(15, false, 40, false)]
    [TestCase(0, false, 1, false)]
    [TestCase(0, true, 4000, true)]
    public async Task EditWindow_OffersEditOnlyWhileTheServerWouldAcceptIt(int window, bool admin, int ageMinutes, bool expected)
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Sender");
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(true);
        using var http = new HttpClient(new Handler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/session") return Task.FromResult(Json(new SessionResponse(user, "token")));
            if (path.EndsWith("initialize")) return Task.FromResult(Json(new ChatDefaults(Guid.NewGuid(), [], window, admin)));
            return Task.FromResult(Json(new ChatPage<Conversation>([], 0)));
        })) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);
        var message = new ChatMessage { Id = Guid.NewGuid(), Mine = true, CreatedAt = DateTime.UtcNow.AddMinutes(-ageMinutes) };
        Assert.That(state.CanEditMessage(message), Is.True, "Unknown rules must leave the server to decide.");
        await state.InitializeAsync();
        Assert.That(state.CanEditMessage(message), Is.EqualTo(expected));
        Assert.That(state.EditExpiry(message).HasValue, Is.EqualTo(window > 0 && !admin));
    }

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle, List<Guid>? deleted = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            request.RequestUri!.AbsolutePath == "/api/chat/conversations/deleted"
                ? Task.FromResult(Json(new ChatPage<Guid>(deleted ?? [], deleted?.Count ?? 0))) : handle(request);
    }
}
