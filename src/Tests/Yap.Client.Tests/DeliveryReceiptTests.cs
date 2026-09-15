using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using Moq;
using NUnit.Framework;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Client.Tests;

/// <summary>Delivered must mean "this device received it", not "the reader opened it".</summary>
public sealed class DeliveryReceiptTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Test]
    public async Task SynchronizeAsync_UnreadThreadNeverOpened_AcknowledgesWithoutSelectingIt()
    {
        await using var f = await Fixture.CreateAsync();
        var message = f.Incoming(f.Thread);
        f.Inbox.Add(f.Summary(f.Thread, message, unread: 1));
        await f.State.SynchronizeAsync();
        await f.DeliveredAsync(message.Id);
        Assert.That(f.State.Selected, Is.Null, "Acknowledgment must not depend on opening the conversation.");
        Assert.That(f.Delivered.Single().ThreadId, Is.EqualTo(f.Thread));
        Assert.That(f.Reads, Is.Empty, "Delivered is not read.");
    }

    [Test]
    public async Task SynchronizeAsync_SeveralUnreadInBackgroundThread_ReadsThePageOnceAndThenCostsNothing()
    {
        await using var f = await Fixture.CreateAsync();
        var history = Enumerable.Range(0, 3).Select(_ => f.Incoming(f.Thread)).ToList();
        f.Messages[f.Thread] = history;
        f.Inbox.Add(f.Summary(f.Thread, history[^1], unread: 3));
        await f.State.SynchronizeAsync();
        await f.DeliveredAsync(history.Select(x => x.Id).ToArray());
        var pages = f.Requests.Count(x => x.Contains($"/conversations/{f.Thread}/messages"));
        Assert.That(pages, Is.EqualTo(1), "One page per thread, not one request per message.");
        f.Requests.Clear(); f.Delivered.Clear();
        await f.State.SynchronizeAsync();
        Assert.That(f.Requests.Any(x => x.Contains($"/conversations/{f.Thread}/messages")), Is.False,
            "A thread already acknowledged through its newest message must cost no further reads.");
        Assert.That(f.Delivered, Is.Empty);
    }

    [Test]
    public async Task SynchronizeAsync_SummaryStillEncrypted_IsNotAcknowledgedUntilItDecrypts()
    {
        await using var f = await Fixture.CreateAsync();
        var message = f.Incoming(f.Thread);
        message.EncryptedEnvelope = "ciphertext"; message.EncryptionPending = true;
        f.Inbox.Add(f.Summary(f.Thread, message, unread: 1));
        await f.State.SynchronizeAsync();
        await Task.Delay(120);
        Assert.That(f.Delivered, Is.Empty, "A message this device cannot read has not been delivered.");
        f.Inbox[0].LastMessage = f.Incoming(f.Thread, message.Id);
        await f.State.SynchronizeAsync();
        await f.DeliveredAsync(message.Id);
    }

    [Test]
    public async Task DeliveredReceipt_InterruptedBeforeAcknowledgement_SurvivesAReload()
    {
        await using var f = await Fixture.CreateAsync(online: false);
        await f.Store.Store.SaveConversationsAsync(f.Scope, [new Conversation { Id = f.Thread }]);
        await using (var first = new ChatState(f.Store.Store, new ChatApi(f.Http) { Account = f.Scope }, f.Js.Object))
        {
            await first.InitializeAsync();
            var message = f.Incoming(f.Thread);
            f.RejectDelivered = true;
            await first.ChatSocketEvent(first.Scope, f.Push(message));
            for (var i = 0; i < 100 && f.Attempts == 0; i++) await Task.Delay(10);
            Assert.That(f.Delivered, Is.Empty);
            f.Inbox.Add(f.Summary(f.Thread, message, unread: 1));
            f.PendingId = message.Id;
        }
        await Task.Delay(50); // Let the rejected flush finish writing its snapshot.
        f.RejectDelivered = false; f.Online = true;
        await using var second = new ChatState(f.Store.Store, new ChatApi(f.Http) { Account = f.Scope }, f.Js.Object);
        await second.InitializeAsync();
        await second.SynchronizeAsync();
        await f.DeliveredAsync(f.PendingId);
    }

    [Test]
    public async Task ChatSocketEvent_ThreadReplyTakesTheReconcilePath_StillAcknowledgesTheReply()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Store.Store.SaveConversationsAsync(f.Scope, [new Conversation { Id = f.Thread }]);
        await f.State.SynchronizeAsync();
        var reply = f.Incoming(f.Thread); reply.IsThreadReply = true; reply.ParentId = Guid.NewGuid();
        f.Messages[f.Thread] = [reply];
        // The summary is our own message, so only the deferred reply can produce a receipt.
        f.Inbox.Add(f.Summary(f.Thread, new ChatMessage { Id = Guid.NewGuid(), ThreadId = f.Thread, Mine = true, CreatedAt = DateTime.UtcNow }, unread: 0));
        try { await f.State.ChatSocketEvent(f.State.Scope, f.Push(reply)); }
        catch (HttpRequestException) { /* Reconciliation asks the socket to resynchronize. */ }
        await f.DeliveredAsync(reply.Id);
        Assert.That(f.Requests.Any(x => x.Contains($"ids={reply.Id}")), Is.True,
            "Named IDs reach a reply the main timeline page would not return.");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public required StoreFixture Store { get; init; }
        public required HttpClient Http { get; init; }
        public required ChatState State { get; init; }
        public required UserSession User { get; init; }
        public required Guid Thread { get; init; }
        public required Mock<IJSRuntime> Js { get; init; }
        public List<string> Requests { get; } = [];
        public List<Conversation> Inbox { get; } = [];
        public Dictionary<Guid, List<ChatMessage>> Messages { get; } = [];
        public List<ReadMessages> Delivered { get; } = [];
        public List<ReadMessages> Reads { get; } = [];
        public bool RejectDelivered { get; set; }
        public int Attempts { get; set; }
        public bool Online { get; set; }
        public Guid PendingId { get; set; }
        public string Scope => OfflineStore.Scope(User);

        public ChatMessage Incoming(Guid thread, Guid? id = null) => new()
        { Id = id ?? Guid.NewGuid(), ThreadId = thread, SenderId = Guid.NewGuid(), Text = "From a friend", CreatedAt = DateTime.UtcNow.AddSeconds(Requests.Count) };
        public Conversation Summary(Guid thread, ChatMessage last, int unread) => new()
        { Id = thread, Unread = unread, LastMessage = last, LastMessageAt = last.CreatedAt };
        public string Push(ChatMessage message) => JsonSerializer.Serialize(new ChatSocketEvent(
            Guid.NewGuid(), 1, "MessageCreated", Thread, message.SenderId, [message.Id], [message]), Json);

        /// <summary>Waits for the coalesced flush and asserts exactly these IDs were acknowledged.</summary>
        public async Task DeliveredAsync(params Guid[] ids)
        {
            for (var i = 0; i < 200 && Delivered.Sum(x => x.MessageIds.Count) < ids.Length; i++) await Task.Delay(10);
            Assert.That(Delivered.SelectMany(x => x.MessageIds).Distinct(), Is.EquivalentTo(ids));
        }

        public static async Task<Fixture> CreateAsync(bool online = true)
        {
            var store = await StoreFixture.CreateAsync();
            var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Recipient");
            await store.Store.SetSettingAsync("user", JsonSerializer.Serialize(user));
            var js = new Mock<IJSRuntime>();
            Fixture fixture = null!;
            js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>()))
                .Returns(() => ValueTask.FromResult(fixture.Online));
            var http = new HttpClient(new Handler(async request => await fixture.RespondAsync(request)))
            { BaseAddress = new("https://yap.test/") };
            var api = new ChatApi(http) { Account = OfflineStore.Scope(user) };
            var state = new ChatState(store.Store, api, js.Object);
            fixture = new() { Store = store, Http = http, State = state, User = user, Thread = Guid.NewGuid(), Js = js, Online = online };
            await state.InitializeAsync();
            return fixture;
        }

        private async Task<HttpResponseMessage> RespondAsync(HttpRequestMessage request)
        {
            var uri = request.RequestUri!;
            var path = uri.AbsolutePath;
            Requests.Add(uri.PathAndQuery);
            if (path == "/api/session") return Response(new SessionResponse(User, "token"));
            if (path.EndsWith("initialize")) return Response(new ChatDefaults(Guid.NewGuid(), []));
            if (path.EndsWith("/conversations/deleted")) return Response(new ChatPage<Guid>([], 0));
            if (path.EndsWith("/chat/delivered"))
            {
                Attempts++;
                if (RejectDelivered) throw new HttpRequestException("Disconnected before acknowledgment");
                Delivered.Add((await request.Content!.ReadFromJsonAsync<ReadMessages>())!);
                return new(HttpStatusCode.NoContent);
            }
            if (path.EndsWith("/chat/read")) { Reads.Add((await request.Content!.ReadFromJsonAsync<ReadMessages>())!); return new(HttpStatusCode.NoContent); }
            if (path.EndsWith("/messages"))
            {
                var thread = Guid.Parse(path.Split('/')[^2]);
                var items = Messages.GetValueOrDefault(thread, []);
                var ids = System.Web.HttpUtility.ParseQueryString(uri.Query).GetValues("ids");
                if (ids is not null) items = items.Where(x => ids.Contains(x.Id.ToString())).ToList();
                return Response(new ChatPage<ChatMessage>([.. items], items.Count));
            }
            if (path.EndsWith("/conversations")) return Response(new ChatPage<Conversation>([.. Inbox], Inbox.Count));
            if (path.Contains("/conversations/")) return Response(new Conversation { Id = Guid.Parse(path.Split('/')[^1]) });
            return Response(new { credentialId = Guid.NewGuid() });
        }

        public async ValueTask DisposeAsync() { await State.DisposeAsync(); Http.Dispose(); await Store.DisposeAsync(); }
    }

    private static HttpResponseMessage Response(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body, body.GetType()) };
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => handler(request);
    }
}
