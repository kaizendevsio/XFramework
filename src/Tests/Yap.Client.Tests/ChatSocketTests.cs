using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using Moq;
using NUnit.Framework;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Client.Tests;

public sealed class ChatSocketTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Test]
    public async Task SendAsync_BackgroundRefreshIsBlocked_SendCommitsBeforeRefreshIsReleased()
    {
        await using var store = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Sender");
        var thread = Guid.NewGuid(); var conversation = new Conversation { Id = thread };
        var block = false;
        var refreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(true);
        using var http = new HttpClient(new AsyncHandler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/session") return Response(new SessionResponse(user, "token"));
            if (path.EndsWith("initialize")) return Response(new ChatDefaults(Guid.NewGuid(), []));
            if (request.Method == HttpMethod.Post && path.EndsWith("messages"))
            {
                var message = (await request.Content!.ReadFromJsonAsync<SendMessage>())!;
                Assert.That(release.Task.IsCompleted, Is.False, "Sending must not await the housekeeping response.");
                sent.TrySetResult(); return Response(new MessageReceipt(message.Id));
            }
            if (path.EndsWith(thread.ToString()))
            {
                if (block) { refreshStarted.TrySetResult(); await release.Task; }
                return Response(conversation);
            }
            if (path.EndsWith("messages")) return Response(new ChatPage<ChatMessage>([], 0));
            if (path.EndsWith("deleted")) return Response(new ChatPage<Guid>([], 0));
            return Response(new ChatPage<Conversation>([conversation], 1));
        })) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(store.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync(); await state.SelectAsync(thread);
        block = true;
        var refresh = state.RefreshHint();
        try
        {
            await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await state.SendAsync(thread, "Send independently", null, "main");
            await sent.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.That(refresh.IsCompleted, Is.False);
        }
        finally { release.TrySetResult(); }
        await refresh.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Test]
    public async Task PostAsync_ConnectedSocketReturnsTypedReceipt_WithoutHttp()
    {
        var httpCalls = 0; var socketCalls = 0;
        using var http = Client(_ => { httpCalls++; return new(HttpStatusCode.InternalServerError); });
        var message = new SendMessage(Guid.NewGuid(), Guid.NewGuid(), "ciphertext reference");
        var api = new ChatApi(http) { SocketRequest = (operation, body, ct) =>
        {
            socketCalls++;
            Assert.That(operation, Is.EqualTo("send")); Assert.That(body, Is.SameAs(message));
            return Task.FromResult<ChatSocketResponse?>(new(200, JsonSerializer.SerializeToElement(new MessageReceipt(message.Id), Json)));
        } };
        var result = await api.PostAsync<MessageReceipt>("api/chat/messages", message);
        Assert.That(result!.MessageId, Is.EqualTo(message.Id));
        Assert.That(socketCalls, Is.EqualTo(1)); Assert.That(httpCalls, Is.Zero);
    }

    [TestCase(401)]
    [TestCase(409)]
    [TestCase(503)]
    public void PostAsync_SocketRejects_DoesNotFallbackOrReplay(int status)
    {
        var httpCalls = 0; var socketCalls = 0;
        using var http = Client(_ => { httpCalls++; return new(HttpStatusCode.OK); });
        var api = new ChatApi(http) { SocketRequest = (_, _, _) =>
        { socketCalls++; return Task.FromResult<ChatSocketResponse?>(new(status)); } };
        var error = Assert.ThrowsAsync<ChatApiException>(() => api.PostAsync<MessageReceipt>("api/chat/messages", new { id = Guid.NewGuid() }));
        Assert.That(error!.Status, Is.EqualTo(status)); Assert.That(socketCalls, Is.EqualTo(1)); Assert.That(httpCalls, Is.Zero);
    }

    [Test]
    public void PostAsync_SocketAcceptanceUncertain_DoesNotFallbackOrReplay()
    {
        var httpCalls = 0; var socketCalls = 0;
        using var http = Client(_ => { httpCalls++; return new(HttpStatusCode.OK); });
        var api = new ChatApi(http) { SocketRequest = (_, _, _) =>
        { socketCalls++; throw new HttpRequestException("Connection lost after commit"); } };
        Assert.ThrowsAsync<HttpRequestException>(() => api.PostAsync<MessageReceipt>("api/chat/messages", new { id = Guid.NewGuid() }));
        Assert.That(socketCalls, Is.EqualTo(1)); Assert.That(httpCalls, Is.Zero);
    }

    [Test]
    public async Task PostAsync_SocketUnavailableBeforeSend_UsesExistingAccountBoundHttpPathOnce()
    {
        var httpCalls = 0; var id = Guid.NewGuid();
        using var http = Client(request =>
        {
            httpCalls++; Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(request.Headers.GetValues("X-Yap-Account"), Is.EqualTo(new[] { "scope" }));
            Assert.That(request.Headers.GetValues("RequestVerificationToken"), Is.EqualTo(new[] { "token" }));
            return Response(new MessageReceipt(id));
        });
        var api = new ChatApi(http) { Account = "scope", Token = "token", SocketRequest = (_, _, _) => Task.FromResult<ChatSocketResponse?>(null) };
        Assert.That((await api.PostAsync<MessageReceipt>("api/chat/messages", new { id }))!.MessageId, Is.EqualTo(id));
        Assert.That(httpCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task PostAsync_AccountSwitchDuringSocketAvailabilityCheck_CannotFallbackUnderNewIdentity()
    {
        var httpCalls = 0;
        var checkedSocket = new TaskCompletionSource<ChatSocketResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = Client(_ => { httpCalls++; return new(HttpStatusCode.OK); });
        var api = new ChatApi(http) { Account = "first", SocketRequest = (_, _, _) => checkedSocket.Task };
        var send = api.PostAsync<MessageReceipt>("api/chat/messages", new { id = Guid.NewGuid() });
        api.Account = "second"; checkedSocket.SetResult(null);
        Assert.ThrowsAsync<OperationCanceledException>(async () => await send);
        Assert.That(httpCalls, Is.Zero);
    }

    [Test]
    public async Task ChatSocketEvent_NewerPushDuringHttpRefresh_CannotBeOverwrittenByOlderSnapshot()
    {
        await using var store = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Recipient"); var thread = Guid.NewGuid();
        var message = new ChatMessage { Id = Guid.NewGuid(), ThreadId = thread, Mine = true, Text = "Newer push", CreatedAt = DateTime.UtcNow };
        var history = new List<ChatMessage>(); var block = false;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(true);
        using var http = new HttpClient(new AsyncHandler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/session") return Response(new SessionResponse(user, "token"));
            if (path.EndsWith("initialize")) return Response(new ChatDefaults(Guid.NewGuid(), []));
            if (path.EndsWith(thread.ToString()))
            {
                if (block) { started.TrySetResult(); await release.Task; }
                return Response(new Conversation { Id = thread });
            }
            if (path.EndsWith("messages")) return Response(new ChatPage<ChatMessage>(history.ToList(), history.Count));
            if (path.EndsWith("deleted")) return Response(new ChatPage<Guid>([], 0));
            return Response(new ChatPage<Conversation>([new() { Id = thread, LastMessage = history.LastOrDefault(), LastMessageAt = history.LastOrDefault()?.CreatedAt }], 1));
        })) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(store.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync(); await state.SelectAsync(thread); block = true;
        var refresh = state.RefreshHint();
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await state.ChatSocketEvent(state.Scope, JsonSerializer.Serialize(new ChatSocketEvent(Guid.NewGuid(), 1,
                "MessageCreated", thread, user.CredentialId, [message.Id], [message]), Json));
            Assert.That(state.Selected!.Messages.Single().Id, Is.EqualTo(message.Id));
            history.Add(message);
        }
        finally { block = false; release.TrySetResult(); }
        await refresh.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.That(state.Selected!.Messages.Single().Text, Is.EqualTo(message.Text));
        Assert.That((await store.Store.MessageAsync(state.Scope, message.Id))!.Text, Is.EqualTo(message.Text));
    }

    [Test]
    public async Task ChatSocketEvent_DirectPayloadIsPersistedWithoutMessageOrInboxGet_AndDuplicateIsIdempotent()
    {
        await using var f = await Fixture.CreateAsync();
        await f.State.SelectAsync(f.Thread);
        var message = f.Message(); message.Mine = true;
        var push = f.Push(message);
        await f.State.ChatSocketEvent(f.Scope, push);
        await f.State.ChatSocketEvent(f.Scope, push);
        Assert.That(f.Requests, Is.Empty);
        Assert.That(f.State.Selected!.Messages.Select(x => x.Id), Is.EqualTo(new[] { message.Id }));
        Assert.That(f.State.Selected.MessageTotal, Is.EqualTo(1));
        Assert.That(f.State.Conversations.Single().Preview, Is.EqualTo(message.Text));
        Assert.That((await f.Store.Store.MessageAsync(f.Scope, message.Id))!.Text, Is.EqualTo(message.Text));
        Assert.That((await f.Store.Store.ConversationsAsync(f.Scope)).Single().Preview, Is.EqualTo(message.Text));
    }

    [Test]
    public async Task ChatSocketEvent_DuplicateIncomingPayload_DoesNotIncreaseUnreadTwice()
    {
        await using var f = await Fixture.CreateAsync();
        var message = f.Message(); var push = f.Push(message);
        f.Api.SocketRequest = (_, _, _) => Task.FromResult<ChatSocketResponse?>(new(204));
        await f.State.ChatSocketEvent(f.Scope, push); await f.State.ChatSocketEvent(f.Scope, push);
        Assert.That(f.State.Conversations.Single().Unread, Is.EqualTo(1));
        Assert.That((await f.Store.Store.MessagesAsync(f.Scope, f.Thread)).Count, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ChatSocketEvent_EncryptedPayload_DeliversOnlyAfterDecryptionAndPersistence(bool locked)
    {
        await using var f = await Fixture.CreateAsync();
        var decryptStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var decrypt = new TaskCompletionSource<ChatEncryption.EncryptedMessageContent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Js.Setup(x => x.InvokeAsync<ChatEncryption.EncryptedMessageContent>("yap.encryption.decrypt", It.IsAny<object?[]?>()))
            .Returns(() => { decryptStarted.TrySetResult(); return new ValueTask<ChatEncryption.EncryptedMessageContent>(decrypt.Task); });
        var message = f.Message(); message.EncryptedEnvelope = "signed ciphertext";
        var deliveryCount = 0;
        f.Api.SocketRequest = async (operation, body, ct) =>
        {
            Assert.That(operation, Is.EqualTo("delivered"));
            var receipt = (ReadMessages)body!; Assert.That(receipt.MessageIds, Is.EqualTo(new[] { message.Id }));
            var saved = await f.Store.Store.MessageAsync(f.Scope, message.Id);
            Assert.That(saved, Is.Not.Null); Assert.That(saved!.EncryptionLocked, Is.False);
            Assert.That(saved.Text, Is.EqualTo("Verified plaintext"));
            deliveryCount++; delivered.TrySetResult(); return new(204);
        };
        var applying = f.State.ChatSocketEvent(f.Scope, f.Push(message));
        await decryptStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.That(deliveryCount, Is.Zero); Assert.That(await f.Store.Store.MessageAsync(f.Scope, message.Id), Is.Null);
        if (locked) decrypt.SetException(new JSException("Key unavailable"));
        else decrypt.SetResult(new("Verified plaintext", []));
        await applying;
        if (locked)
        {
            await Task.Delay(80);
            Assert.That(deliveryCount, Is.Zero);
            Assert.That((await f.Store.Store.MessageAsync(f.Scope, message.Id))!.EncryptionLocked, Is.True);
        }
        else await delivered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.That(f.Requests, Has.All.Contains("/encryption/people/"), "The payload replaces message/inbox GETs; sender verification still applies.");
    }

    [Test]
    public async Task ChatSocketEvent_StaleAccount_RejectsBeforeChangingStorageOrUi()
    {
        await using var f = await Fixture.CreateAsync(); var message = f.Message();
        Assert.ThrowsAsync<OperationCanceledException>(() => f.State.ChatSocketEvent("other-account", f.Push(message)));
        Assert.That(await f.Store.Store.MessageAsync(f.Scope, message.Id), Is.Null);
        Assert.That(f.State.Conversations.Single().Unread, Is.Zero); Assert.That(f.Requests, Is.Empty);
    }

    [Test]
    public async Task SynchronizeAsync_FailedDeliveredReceipt_RetriesWithoutOpeningConversationOrReceivingAnotherMessage()
    {
        await using var f = await Fixture.CreateAsync();
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var retried = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        f.Api.SocketRequest = (operation, body, ct) =>
        {
            if (operation != "delivered") return Task.FromResult<ChatSocketResponse?>(null);
            if (++attempts == 1) { failed.TrySetResult(); throw new HttpRequestException("Disconnected before acknowledgment"); }
            retried.TrySetResult(); return Task.FromResult<ChatSocketResponse?>(new(204));
        };
        await f.State.ChatSocketEvent(f.Scope, f.Push(f.Message()));
        await failed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await Task.Delay(30); // Let the failed receipt task finish its diagnostic callback.
        f.Js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(true);
        await f.State.SynchronizeAsync();
        await retried.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.That(attempts, Is.EqualTo(2)); Assert.That(f.State.Selected, Is.Null);
    }

    private static HttpResponseMessage Response(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body, body.GetType()) };
    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> handler) => new(new Handler(handler)) { BaseAddress = new("https://yap.test/") };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(handler(request));
    }
    private sealed class AsyncHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => handler(request);
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public required StoreFixture Store { get; init; }
        public required HttpClient Http { get; init; }
        public required ChatApi Api { get; init; }
        public required ChatState State { get; init; }
        public required UserSession User { get; init; }
        public required Guid Thread { get; init; }
        public required Mock<IJSRuntime> Js { get; init; }
        public required List<string> Requests { get; init; }
        public string Scope => OfflineStore.Scope(User);
        public ChatMessage Message() => new() { Id = Guid.NewGuid(), ThreadId = Thread, SenderId = Guid.NewGuid(), Text = "A direct push", CreatedAt = DateTime.UtcNow };
        public string Push(ChatMessage message) => JsonSerializer.Serialize(new ChatSocketEvent(Guid.NewGuid(), 1, "MessageCreated", Thread, message.SenderId, [message.Id], [message]), Json);
        public static async Task<Fixture> CreateAsync()
        {
            var store = await StoreFixture.CreateAsync();
            var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Recipient"); var thread = Guid.NewGuid();
            var scope = OfflineStore.Scope(user); var requests = new List<string>(); var js = new Mock<IJSRuntime>();
            js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(false);
            await store.Store.SetSettingAsync("user", JsonSerializer.Serialize(user));
            await store.Store.SaveConversationsAsync(scope, [new Conversation { Id = thread }]);
            var http = Client(request =>
            {
                var path = request.RequestUri!.PathAndQuery; requests.Add(path);
                if (path == "/api/session") return Response(new SessionResponse(user, "token"));
                if (path.EndsWith("initialize")) return Response(new ChatDefaults(Guid.NewGuid(), []));
                if (path.Contains("/conversations/deleted")) return Response(new ChatPage<Guid>([], 0));
                if (path.Contains("/conversations")) return Response(new ChatPage<Conversation>([new() { Id = thread }], 1));
                return Response(new { credentialId = Guid.NewGuid() });
            });
            var api = new ChatApi(http) { Account = scope };
            var state = new ChatState(store.Store, api, js.Object); await state.InitializeAsync();
            return new() { Store = store, User = user, Thread = thread, Requests = requests, Js = js, Http = http, Api = api, State = state };
        }
        public async ValueTask DisposeAsync() { await State.DisposeAsync(); Http.Dispose(); await Store.DisposeAsync(); }
    }
}
