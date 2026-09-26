using System.Net;
using System.Net.Http.Json;
using Microsoft.JSInterop;
using Moq;
using NUnit.Framework;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Client.Tests;

public sealed class SavedMessagesTests
{
    [Test]
    public async Task SavedMessagesAsync_LocksAnUndecryptableRow_AndNeverShowsTheEnvelope()
    {
        var thread = Guid.NewGuid();
        var envelope = "-----BEGIN PGP MESSAGE-----ciphertext";
        var encrypted = Saved(thread, "Encrypted message", envelope);
        var plain = Saved(thread, "Ship it on Friday");
        await using var fixture = await StateFixture.CreateAsync(request => Task.FromResult(request.RequestUri!.AbsolutePath == "/api/chat/saved"
            ? StateFixture.Json(new ChatPage<SavedMessage>([encrypted, plain], 2)) : null));

        var page = await fixture.State.SavedMessagesAsync();

        Assert.That(page.TotalCount, Is.EqualTo(2));
        var locked = page.Items.Single(x => x.Message.Id == encrypted.Message.Id);
        Assert.Multiple(() =>
        {
            Assert.That(locked.Message.EncryptionLocked, Is.True);
            Assert.That(locked.Message.Text, Does.Not.Contain("ciphertext"));
            Assert.That(locked.Message.Text, Does.Contain("Encrypted message"));
            Assert.That(page.Items.Single(x => x.Message.Id == plain.Message.Id).Message.Text, Is.EqualTo("Ship it on Friday"));
        });
        // The page is cached so the tab still opens with no connection.
        var cached = await fixture.Store.SavedMessagesAsync(fixture.State.Scope);
        Assert.That(cached.Select(x => x.Id), Is.EquivalentTo(page.Items.Select(x => x.Message.Id)));
    }

    [Test]
    public async Task SavedMessagesAsync_WhenTheListCannotBeReached_FallsBackToTheDeviceCache()
    {
        var thread = Guid.NewGuid();
        await using var fixture = await StateFixture.CreateAsync(request => request.RequestUri!.AbsolutePath == "/api/chat/saved"
            ? throw new HttpRequestException("offline") : Task.FromResult<HttpResponseMessage?>(null));
        var bookmarked = new ChatMessage { Id = Guid.NewGuid(), ThreadId = thread, Text = "keep this", Saved = true, CreatedAt = DateTime.UtcNow };
        var ordinary = new ChatMessage { Id = Guid.NewGuid(), ThreadId = thread, Text = "not saved", CreatedAt = DateTime.UtcNow };
        await fixture.Store.SaveMessagesAsync(fixture.State.Scope, [bookmarked, ordinary]);

        var page = await fixture.State.SavedMessagesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.State.Online, Is.False);
            Assert.That(page.TotalCount, Is.EqualTo(1));
            Assert.That(page.Items.Single().Message.Id, Is.EqualTo(bookmarked.Id));
            Assert.That(page.Items.Single().Message.Text, Is.EqualTo("keep this"));
        });
    }

    [Test]
    public async Task UnsaveAsync_ClearsTheBookmarkOnTheDeviceSoTheRowStaysGoneOffline()
    {
        var thread = Guid.NewGuid();
        var posted = new List<MessageAction>();
        await using var fixture = await StateFixture.CreateAsync(async request =>
        {
            if (request.RequestUri!.AbsolutePath != "/api/chat/message-actions") return null;
            posted.Add((await request.Content!.ReadFromJsonAsync<MessageAction>())!);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var bookmarked = new ChatMessage { Id = Guid.NewGuid(), ThreadId = thread, Text = "keep this", Saved = true, CreatedAt = DateTime.UtcNow };
        await fixture.Store.SaveMessagesAsync(fixture.State.Scope, [bookmarked]);

        Assert.That(await fixture.State.UnsaveAsync(bookmarked), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(posted.Single().Action, Is.EqualTo("unsave"));
            Assert.That(posted.Single().MessageId, Is.EqualTo(bookmarked.Id));
            Assert.That(bookmarked.Saved, Is.False);
        });
        Assert.That(await fixture.Store.SavedMessagesAsync(fixture.State.Scope), Is.Empty);
        Assert.That(await fixture.Store.SavedMessageCountAsync(fixture.State.Scope), Is.Zero);
    }

    [Test]
    public async Task SavedMessagesAsync_BeforeTheSessionBinds_ServesTheCacheInsteadOfAnUnauthorizedRead()
    {
        var reads = 0;
        await using var fixture = await StateFixture.CreateAsync(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/chat/saved") reads++;
            return Task.FromResult<HttpResponseMessage?>(null);
        }, holdSession: true);
        var bookmarked = new ChatMessage { Id = Guid.NewGuid(), ThreadId = Guid.NewGuid(), Text = "keep this", Saved = true, CreatedAt = DateTime.UtcNow };
        await fixture.Store.SaveMessagesAsync(fixture.State.Scope, [bookmarked]);

        var page = await fixture.State.SavedMessagesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.State.SessionReady, Is.False);
            Assert.That(reads, Is.Zero, "A cold start must not issue an account-scoped read before the account is bound.");
            Assert.That(page.Items.Single().Message.Id, Is.EqualTo(bookmarked.Id));
        });
    }

    [Test]
    public async Task SwitchingBackToSaved_ReusesTheRecentFirstPage_UntilABookmarkChanges()
    {
        var thread = Guid.NewGuid();
        var reads = 0;
        var row = Saved(thread, "Ship it on Friday");
        await using var fixture = await StateFixture.CreateAsync(request => request.RequestUri!.AbsolutePath switch
        {
            "/api/chat/saved" => Task.FromResult<HttpResponseMessage?>(StateFixture.Json(new ChatPage<SavedMessage>([row], ++reads))),
            "/api/chat/message-actions" => Task.FromResult<HttpResponseMessage?>(new HttpResponseMessage(HttpStatusCode.NoContent)),
            _ => Task.FromResult<HttpResponseMessage?>(null)
        });

        var first = await fixture.State.SavedMessagesAsync();
        first.Items.Clear();
        var again = await fixture.State.SavedMessagesAsync();
        Assert.Multiple(() =>
        {
            Assert.That(reads, Is.EqualTo(1), "A tab switch must not re-download, re-decrypt and re-store the same page.");
            Assert.That(again.Items.Single().Message.Id, Is.EqualTo(row.Message.Id), "A caller changing its list cannot empty the reused page.");
        });

        Assert.That(await fixture.State.UnsaveAsync(row.Message), Is.True);
        await fixture.State.SavedMessagesAsync();
        Assert.That(reads, Is.EqualTo(2), "Changing a bookmark here reads the server again.");
        await fixture.State.SavedMessagesAsync(1);
        Assert.That(reads, Is.EqualTo(3), "Only the first page is reused.");
    }

    [Test]
    public async Task SwitchingBackToCalls_ReusesTheRecentFirstPage()
    {
        var reads = 0;
        var call = new CallHistoryItem(new() { Id = Guid.NewGuid(), ThreadId = Guid.NewGuid(), Text = "Voice call · 1:05", IsCallSummary = true }, "Alex", false);
        await using var fixture = await StateFixture.CreateAsync(request => Task.FromResult(request.RequestUri!.AbsolutePath == "/api/chat/calls/history"
            ? StateFixture.Json(new ChatPage<CallHistoryItem>([call], ++reads)) : null));

        await fixture.State.CallsAsync();
        var again = await fixture.State.CallsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(reads, Is.EqualTo(1));
            Assert.That(again.Items.Single().Message.Id, Is.EqualTo(call.Message.Id));
        });
    }

    private static SavedMessage Saved(Guid thread, string text, string? envelope = null) => new(
        new ChatMessage { Id = Guid.NewGuid(), ThreadId = thread, Text = text, Saved = true, EncryptedEnvelope = envelope, CreatedAt = DateTime.UtcNow },
        "Sarah Mensah", false, DateTime.UtcNow);

    /// <summary>A signed-in <see cref="ChatState"/> whose network answers only what the test cares about.</summary>
    private sealed class StateFixture(StoreFixture store, ChatState state, HttpClient http, TaskCompletionSource release) : IAsyncDisposable
    {
        public OfflineStore Store { get; } = store.Store;
        public ChatState State { get; } = state;

        /// <param name="holdSession">
        /// Stalls the session read so the account never binds. That is the cold-start window:
        /// the signed-in user is restored from the device while the sync is still in flight.
        /// </param>
        public static async Task<StateFixture> CreateAsync(Func<HttpRequestMessage, Task<HttpResponseMessage?>> route, bool holdSession = false)
        {
            var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Jamie Davis");
            var store = await StoreFixture.CreateAsync();
            if (holdSession) await store.Store.SetSettingAsync("user", System.Text.Json.JsonSerializer.Serialize(user));
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var js = new Mock<IJSRuntime>();
            js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(true);
            var http = new HttpClient(new Handler(async request => await route(request) ?? request.RequestUri!.AbsolutePath switch
            {
                "/api/session" when holdSession => await Held(release),
                "/api/session" => Json(new SessionResponse(user, "token")),
                "/api/chat/initialize" => Json(new ChatDefaults(Guid.NewGuid(), [])),
                "/api/chat/conversations/deleted" => Json(new ChatPage<Guid>([], 0)),
                "/api/chat/conversations" => Json(new ChatPage<Conversation>([], 0)),
                // A sender directory the device cannot fetch is exactly how an undecryptable row happens.
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            })) { BaseAddress = new("https://yap.test/") };
            var state = new ChatState(store.Store, new ChatApi(http), js.Object);
            await state.InitializeAsync();
            // InitializeAsync hands off to a background sync; account-scoped reads wait for it.
            if (!holdSession) await state.SynchronizeAsync();
            return new StateFixture(store, state, http, release);
        }

        private static async Task<HttpResponseMessage> Held(TaskCompletionSource release)
        { await release.Task; return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable); }

        public static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
        public async ValueTask DisposeAsync()
        { release.TrySetResult(); await State.DisposeAsync(); http.Dispose(); await store.DisposeAsync(); }

        private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => handle(request);
        }
    }
}
