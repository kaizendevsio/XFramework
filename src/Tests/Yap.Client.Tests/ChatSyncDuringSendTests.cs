using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using Moq;
using NUnit.Framework;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Client.Tests;

public sealed class ChatSyncDuringSendTests
{
    [Test]
    public async Task SynchronizeAsync_ExistingSendIsBlocked_ReconnectRefreshCompletesBeforeSendDrain()
    {
        await using var store = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Sender"); var thread = Guid.NewGuid();
        var sendStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSend = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var syncReachedInbox = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observeSync = false;
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(true);
        using var http = new HttpClient(new Handler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/session") return Json(new SessionResponse(user, "token"));
            if (path.EndsWith("initialize")) return Json(new ChatDefaults(Guid.NewGuid(), []));
            if (request.Method == HttpMethod.Post && path.EndsWith("messages"))
            {
                var message = (await request.Content!.ReadFromJsonAsync<SendMessage>())!;
                sendStarted.TrySetResult(); await releaseSend.Task;
                return Json(new MessageReceipt(message.Id));
            }
            if (path.EndsWith("deleted")) return Json(new ChatPage<Guid>([], 0));
            if (path.EndsWith(thread.ToString())) return Json(new Conversation { Id = thread });
            if (path.EndsWith("messages")) return Json(new ChatPage<ChatMessage>([], 0));
            if (observeSync) syncReachedInbox.TrySetResult();
            return Json(new ChatPage<Conversation>([new() { Id = thread }], 1));
        })) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(store.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync(); await state.SelectAsync(thread);
        await state.SendAsync(thread, "A send whose response is delayed", null, "main");
        await sendStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        observeSync = true;
        var synchronize = state.SynchronizeAsync();
        try
        {
            await syncReachedInbox.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var reconnect = state.ChatSocketEvent(state.Scope,
                JsonSerializer.Serialize(new ChatSocketEvent(Guid.NewGuid(), 1, "refresh"), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            await reconnect.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.That(releaseSend.Task.IsCompleted, Is.False);
            Assert.That(synchronize.IsCompleted, Is.False, "Synchronize still waits for drain completion, without owning the receive gate.");
            Assert.That((await store.Store.PendingAsync(state.Scope)).Count, Is.EqualTo(1));
        }
        finally { releaseSend.TrySetResult(); }
        await synchronize.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.That(await store.Store.PendingAsync(state.Scope), Is.Empty);
    }

    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value, value.GetType()) };
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => handler(request);
    }
}
