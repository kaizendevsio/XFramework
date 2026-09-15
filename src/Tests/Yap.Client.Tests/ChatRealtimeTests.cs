using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using Moq;
using NUnit.Framework;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Client.Tests;

public sealed class ChatRealtimeTests
{
    [Test]
    public async Task TargetedUpdates_AreIdempotent_MoveReceipts_AndAvoidInboxReads()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Tester");
        var scope = OfflineStore.Scope(user); var reader = new Person(Guid.NewGuid(), "Reader", "");
        var thread = Guid.NewGuid(); var chat = new Conversation { Id = thread, People = [reader] };
        var old = new ChatMessage { Id = Guid.NewGuid(), ThreadId = thread, CreatedAt = DateTime.UtcNow.AddMinutes(-1), Mine = true, Text = "old", LatestReaders = [reader], IsLatestOwnMessage = true };
        var next = new ChatMessage { Id = Guid.NewGuid(), ThreadId = thread, CreatedAt = DateTime.UtcNow, Mine = true, Text = "new", IsLatestOwnMessage = true };
        var reads = new List<string>(); var updates = new List<ChatMessage> { next };
        var js = new Mock<IJSRuntime>(); js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(true);
        using var http = new HttpClient(new Handler(request =>
        {
            var path = request.RequestUri!.PathAndQuery; reads.Add(path);
            object response = path switch {
                "/api/session" => new SessionResponse(user, "token"),
                "/api/chat/initialize" => new ChatDefaults(Guid.NewGuid(), []),
                _ when path.Contains("ids=") => new ChatPage<ChatMessage>(updates, updates.Count),
                _ when path.Contains("/messages") => new ChatPage<ChatMessage>([old], 1),
                _ when path.EndsWith(thread.ToString()) => chat,
                _ when path.StartsWith("/api/chat/conversations/deleted") => new ChatPage<Guid>([], 0),
                _ => new ChatPage<Conversation>([chat], 1)
            };
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(response, response.GetType()) };
        })) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync(); await state.SelectAsync(thread); reads.Clear();
        Assert.That(state.Selected!.MessageTotal, Is.EqualTo(1), "Initial history total");
        var hint = JsonSerializer.Serialize(new ChatUpdateHint(thread, "MessageCreated", reader.Id, [next.Id]));
        await state.ChatEvent(scope, hint); await state.ChatEvent(scope, hint);
        Assert.That(state.Selected!.Messages.Count, Is.EqualTo(2));
        Assert.That(state.Selected.MessageTotal, Is.EqualTo(2));
        Assert.That(state.Selected.Messages.Single(m => m.Id == old.Id).IsLatestOwnMessage, Is.False);
        Assert.That(state.Conversations.Single().Preview, Is.EqualTo("new"));
        next.LatestReaders = [reader]; next.Readers = [reader]; next.ReadCount = 1;
        await state.ChatEvent(scope, JsonSerializer.Serialize(new ChatUpdateHint(thread, "MessagesRead", reader.Id, [next.Id])));
        Assert.That(state.Selected.Messages.Single(m => m.Id == old.Id).LatestReaders, Is.Empty);
        Assert.That(state.Selected.Messages.Single(m => m.Id == next.Id).LatestReaders.Single().Id, Is.EqualTo(reader.Id));
        Assert.That((await fixture.Store.MessageAsync(scope, old.Id))!.LatestReaders, Is.Empty);
        Assert.That(reads, Has.All.Contains("ids="));
        next.Text = "edited";
        await state.ChatEvent(scope, JsonSerializer.Serialize(new ChatUpdateHint(thread, "MessageEdited", reader.Id, [next.Id])));
        Assert.That(state.Selected.Messages.Single(m => m.Id == next.Id).Text, Is.EqualTo("edited"));
        Assert.That(state.Conversations.Single().Preview, Is.EqualTo("edited"));
        reads.Clear();
        await state.ChatEvent("another-account", hint);
        await state.ChatEvent(scope, JsonSerializer.Serialize(new ChatUpdateHint(thread, "MessagesRead", user.CredentialId, [next.Id])));
        Assert.That(reads, Is.Empty);
        var historical = new ChatMessage { Id = Guid.NewGuid(), ThreadId = thread, Text = "uncached history", CreatedAt = DateTime.UtcNow.AddDays(-1) };
        updates.Clear(); updates.Add(historical);
        await state.ChatEvent(scope, JsonSerializer.Serialize(new ChatUpdateHint(thread, "MessagesRead", reader.Id, [historical.Id])));
        Assert.That(await fixture.Store.MessageAsync(scope, historical.Id), Is.Null);
        Assert.That(state.Selected!.Messages.Count, Is.EqualTo(2));
        await state.ChatEvent(scope, "refresh");
        Assert.That(reads, Has.Some.Contains("conversations?page="));
        Assert.That(state.Selected!.Messages.Select(m => m.Id), Is.EqualTo(new[] { old.Id }), "Reconciliation removes a deleted message");
        old.Text = "edited while disconnected";
        await state.ChatEvent(scope, "refresh");
        Assert.That(state.Selected.Messages.Single().Text, Is.EqualTo(old.Text));
        updates.Clear(); reads.Clear();
        await state.ChatEvent(scope, hint);
        Assert.That(reads, Has.Some.Contains("conversations?page="), "An inaccessible target must reconcile visibility");
        var queued = new ChatMessage { Id = next.Id, ThreadId = thread, Text = "local pending body", Delivery = "Queued" };
        await fixture.Store.QueueAsync(new() { Scope = scope, Id = next.Id, ThreadId = thread, Text = queued.Text, Paused = true }, queued, "main");
        updates.Add(next);
        await state.ChatEvent(scope, hint);
        Assert.That((await fixture.Store.MessageAsync(scope, next.Id))!.Text, Is.EqualTo(queued.Text), "An event cannot overwrite the persisted outbox body");
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> action) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(action(request));
    }
}
