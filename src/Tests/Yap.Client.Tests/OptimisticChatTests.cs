using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using Moq;
using NUnit.Framework;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Client.Tests;

public sealed class OptimisticChatTests
{
    [Test]
    public async Task OpeningAndBurstSend_RenderBeforeLocalStorageCompletes()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var store = fixture.Store;
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Sender");
        var chat = new Conversation { Id = Guid.NewGuid(), Name = "Our chat" };
        var scope = OfflineStore.Scope(user);
        await store.SetSettingAsync("user", JsonSerializer.Serialize(user));
        await store.SaveConversationsAsync(scope, [chat]);
        using var http = new HttpClient(new Handler(_ => throw new AssertionException("Offline work must not use HTTP"))) { BaseAddress = new("https://yap.test/") };
        var js = new Mock<IJSRuntime>();
        await using var state = new ChatState(store, new ChatApi(http), js.Object);
        await state.InitializeAsync();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var storage = store.UseAsync(async _ => { await release.Task; return true; });
        var opening = state.SelectAsync(chat.Id);
        var favorite = state.ToggleFavoriteAsync(state.Selected!);
        var sends = Enumerable.Range(0, 4).Select(i => state.SendAsync(chat.Id, $"Message {i}", null, "main")).ToArray();
        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(state.Selected!.Name, Is.EqualTo(chat.Name));
                Assert.That(state.OpeningConversation, Is.True);
                Assert.That(state.Selected.IsFavorite, Is.True);
                Assert.That(opening.IsCompleted, Is.False);
                Assert.That(sends.All(x => !x.IsCompleted), Is.True);
                Assert.That(state.Selected.Messages.Select(x => x.Text), Is.EqualTo(Enumerable.Range(0, 4).Select(i => $"Message {i}")));
            });
        }
        finally { release.TrySetResult(); await storage; await Task.WhenAll(sends.Append(opening).Append(favorite)); }
        Assert.That(state.Selected!.Messages, Has.Count.EqualTo(4), "A cache read started before the sends must not erase their bubbles.");
        Assert.That(await store.PendingAsync(scope), Has.Count.EqualTo(4));
        Assert.That((await store.ConversationsAsync(scope)).Single().IsFavorite, Is.True);
        Assert.That(state.OpeningConversation, Is.False);
    }

    [Test]
    public async Task FailedOutboxSave_RemovesOnlyFailedBubble_AndKeepsDraft()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var factory = new FailingFactory(fixture);
        var store = new OfflineStore(factory);
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Sender");
        var chat = new Conversation { Id = Guid.NewGuid() };
        var scope = OfflineStore.Scope(user);
        await store.SetSettingAsync("user", JsonSerializer.Serialize(user));
        await store.SaveConversationsAsync(scope, [chat]);
        await store.SaveDraftAsync(scope, "main", "Keep this draft");
        using var http = new HttpClient { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(store, new ChatApi(http), Mock.Of<IJSRuntime>());
        await state.InitializeAsync(); await state.SelectAsync(chat.Id);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var storage = store.UseAsync(async _ => { await release.Task; return true; });
        var send = state.SendAsync(chat.Id, "Keep this draft", null, "main");
        Assert.That(state.Selected!.Messages.Single().Text, Is.EqualTo("Keep this draft"));
        factory.FailNext = true; release.SetResult(); await storage;
        Assert.ThrowsAsync<IOException>(async () => await send);
        Assert.That(state.Selected.Messages, Is.Empty);
        Assert.That(state.PendingCount, Is.Zero);
        Assert.That(await store.DraftAsync(scope, "main"), Is.EqualTo("Keep this draft"));
        factory.FailNext = true;
        Assert.ThrowsAsync<IOException>(() => state.ToggleFavoriteAsync(state.Selected));
        Assert.That(state.Selected.IsFavorite, Is.False);
    }

    [TestCase("edit")]
    [TestCase("react")]
    [TestCase("delete")]
    [TestCase("pin")]
    [TestCase("save")]
    [TestCase("mute")]
    [TestCase("rename")]
    [TestCase("privacy")]
    [TestCase("remove-chat")]
    public async Task SlowRejectedAction_UpdatesImmediately_SurvivesRefresh_ThenRollsBack(string action)
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Sender");
        var chat = new Conversation { Id = Guid.NewGuid(), Name = "Original" };
        var message = new ChatMessage { Id = Guid.NewGuid(), ThreadId = chat.Id, Mine = true, Text = "Original", CreatedAt = DateTime.UtcNow };
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new Handler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("message-actions") || path.EndsWith("thread-actions") || path.EndsWith("conversation-settings"))
            { started.TrySetResult(); await release.Task; return new(HttpStatusCode.Forbidden); }
            if (path == "/api/session") return Json(new SessionResponse(user, "token"));
            if (path.EndsWith("initialize")) return Json(new ChatDefaults(Guid.NewGuid(), [new(Guid.NewGuid(), "Heart", "heart")]));
            if (path.EndsWith("deleted")) return Json(new ChatPage<Guid>([], 0));
            if (path.EndsWith("/messages")) return Json(new ChatPage<ChatMessage>([message], 1));
            if (path.EndsWith(chat.Id.ToString())) return Json(chat);
            return Json(new ChatPage<Conversation>([chat], 1));
        })) { BaseAddress = new("https://yap.test/") };
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(true);
        await using var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync(); await state.SelectAsync(chat.Id);
        var pending = action switch
        {
            "mute" => state.MuteAsync(),
            "rename" => state.UpdateConversationAsync(new(chat.Id, Name: "Changed")),
            "privacy" => state.SetActiveStatusAsync(chat.Id, false),
            "remove-chat" => state.DeleteConversationAsync(chat.Id),
            _ => state.ActionAsync(state.Selected!.Messages.Single(), action, "Changed", "heart")
        };
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        bool Changed() => action switch
        {
            "delete" => state.Selected!.Messages.Count == 0,
            "edit" => state.Selected!.Messages.Single().Text == "Changed",
            "react" => state.Selected!.Messages.Single().Reactions.GetValueOrDefault("heart") == 1,
            "pin" => state.Selected!.Messages.Single().Pinned,
            "save" => state.Selected!.Messages.Single().Saved,
            "mute" => state.Selected!.Muted,
            "rename" => state.Selected!.Name == "Changed",
            "privacy" => !state.Selected!.ShareActiveStatus,
            "remove-chat" => state.Selected!.Removed,
            _ => false
        };
        try
        {
            Assert.That(Changed(), Is.True, "The UI must change while the POST is blocked.");
            if (action != "remove-chat") await state.SynchronizeAsync();
            else Assert.That(await fixture.Store.MessagesAsync(OfflineStore.Scope(user), chat.Id), Has.Count.EqualTo(1), "Keep history until the server confirms deletion.");
            Assert.That(Changed(), Is.True, "Stale server state must not overwrite a pending action.");
        }
        finally { release.TrySetResult(); try { await pending; } catch (ChatApiException) { } }
        Assert.That(Changed(), Is.False, "Rejecting the save restores the previous visible state.");
    }

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handle(request); }
    private sealed class FailingFactory(StoreFixture fixture) : IDbContextFactory<OfflineDatabase>
    {
        public bool FailNext { get; set; }
        public OfflineDatabase CreateDbContext()
        {
            if (FailNext) { FailNext = false; throw new IOException("Disk full"); }
            return fixture.CreateDbContext();
        }
    }
}
