using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using Moq;
using NUnit.Framework;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Client.Tests;

/// <summary>"Sending a photo…": attachment activity rides the typing channel and its setting.</summary>
public sealed class AttachmentActivityTests
{
    private static readonly Guid Thread = Guid.NewGuid(), Sam = Guid.NewGuid(), Alex = Guid.NewGuid();
    private TimeSpan refresh, lifetime, limit;

    [SetUp] public void Remember() { refresh = ChatState.ActivityRefresh; lifetime = ChatState.ActivityLifetime; limit = ChatState.ActivityLimit; }
    [TearDown] public void Restore() { ChatState.ActivityRefresh = refresh; ChatState.ActivityLifetime = lifetime; ChatState.ActivityLimit = limit; }

    [TestCase(ChatActivity.Photo, 1, "sending a photo…")]
    [TestCase(ChatActivity.Photo, 3, "sending 3 photos…")]
    [TestCase(ChatActivity.Video, 1, "sending a video…")]
    [TestCase(ChatActivity.Video, 2, "sending 2 videos…")]
    [TestCase(ChatActivity.File, 1, "sending a file…")]
    [TestCase(ChatActivity.File, 4, "sending 4 files…")]
    [TestCase(ChatActivity.VoiceMessage, 1, "sending a voice message…")]
    [TestCase(ChatActivity.Recording, 0, "recording a voice message…")]
    public void Phrase_NamesOnlyTheKindAndCount(ChatActivity kind, int count, string expected) =>
        Assert.That(ChatState.ActivityPhrase(kind, count), Is.EqualTo(expected));

    [TestCase("image/heic", ChatActivity.Photo)]
    [TestCase("video/mp4", ChatActivity.Video)]
    [TestCase("audio/mpeg", ChatActivity.File)]
    [TestCase("application/pdf", ChatActivity.File)]
    [TestCase(null, ChatActivity.File)]
    public void ContentType_PicksTheKind(string? type, ChatActivity expected) =>
        Assert.That(ChatState.ActivityFor(type), Is.EqualTo(expected));

    [Test]
    public async Task Received_ReplacesTheStatusLine_UntilStoppedOrExpired()
    {
        ChatState.ActivityLifetime = TimeSpan.FromMilliseconds(250);
        await using var state = State(out _, Direct());
        var chat = state.Selected!;
        chat.People = [new(Sam, "Sam Reyes", "sam", ActiveUntil: DateTime.UtcNow.AddSeconds(30))];
        Assert.That(state.ConversationSubtitle(chat), Is.EqualTo("Active now"));

        state.TypingChanged(Thread, Sam, true, ChatActivity.Photo, 2);
        Assert.That(state.ConversationSubtitle(chat), Is.EqualTo("Sending 2 photos…"));
        Assert.That(state.TypingText, Is.Null, "An attachment is not typing.");
        state.TypingChanged(Thread, Sam, false, ChatActivity.Photo, 2);
        Assert.That(state.ActivityText(chat), Is.Null, "Cancel, send or failure withdraws it.");

        state.TypingChanged(Thread, Sam, true, ChatActivity.Recording);
        Assert.That(state.ConversationSubtitle(chat), Is.EqualTo("Recording a voice message…"));
        state.TypingChanged(Thread, Sam, false);
        Assert.That(state.ActivityText(chat), Is.Not.Null, "A typing stop leaves the attachment activity alone.");
        await Task.Delay(400);
        Assert.That(state.ActivityText(chat), Is.Null, "Nothing refreshed it, so it expires like typing.");
        Assert.That(state.ConversationSubtitle(chat), Is.EqualTo("Active now"));

        state.TypingChanged(Thread, Sam, true, ChatActivity.Video);
        await state.ConnectivityChanged(false);
        Assert.That(state.ActivityText(chat), Is.Null, "Going offline forgets it.");
    }

    [Test]
    public async Task Received_InAGroup_NamesWhoIsSending()
    {
        var group = Direct(); group.Group = true;
        group.People = [new(Sam, "Sam Reyes", "sam"), new(Alex, "Alex Kim", "alex")];
        await using var state = State(out _, group);
        state.TypingChanged(Thread, Sam, true, ChatActivity.Photo, 1);
        Assert.That(state.ActivityText(group), Is.EqualTo("Sam is sending a photo…"));
        state.TypingChanged(Thread, Alex, true, ChatActivity.File, 1);
        Assert.That(state.ActivityText(group), Is.EqualTo("Alex and 1 other are sending…"));
    }

    [Test]
    public async Task Received_IsIgnored_WhenTypingIsOffOrItIsSomewhereElse()
    {
        var chat = Direct(); chat.Features &= ~(int)ChatFeature.Typing;
        await using var state = State(out _, chat);
        state.TypingChanged(Thread, Sam, true, ChatActivity.Photo, 1);
        Assert.That(state.ActivityText(chat), Is.Null);
        chat.Features |= (int)ChatFeature.Typing;
        Assert.That(state.ActivityText(chat), Is.Null, "Nothing was kept while it was off.");
        state.TypingChanged(Guid.NewGuid(), Sam, true, ChatActivity.Photo, 1);
        state.TypingChanged(Thread, state.User!.CredentialId, true, ChatActivity.Photo, 1);
        Assert.That(state.ActivityText(chat), Is.Null, "Another conversation's activity and your own never show.");
    }

    [Test]
    public async Task Sent_OnTheTypingChannel_Refreshed_ThenWithdrawn()
    {
        ChatState.ActivityRefresh = TimeSpan.FromMilliseconds(120);
        await using var state = State(out var posts, Direct());
        await state.BeginActivityAsync(Thread, ChatActivity.Photo);
        await state.BeginActivityAsync(Thread, ChatActivity.Photo);
        Assert.That(posts, Has.Count.EqualTo(1), "Repeating the same activity is throttled.");
        Assert.That(posts[0], Is.EqualTo(new ThreadAction(Thread, "typing", true, ChatActivity.Photo, 1)));
        await WaitAsync(() => posts.Count >= 3);
        Assert.That(posts.Skip(1).All(x => x == posts[0]), Is.True, "Refreshes repeat it until it ends.");
        await state.EndActivityAsync(Thread);
        var ended = posts.Count;
        Assert.That(posts[^1], Is.EqualTo(new ThreadAction(Thread, "typing", false, ChatActivity.Photo, 1)));
        await Task.Delay(400);
        Assert.That(posts, Has.Count.EqualTo(ended), "No refresh after it ends.");
        Assert.That(state.OutgoingActivity, Is.Null);
    }

    [Test]
    public async Task Sent_StopsItself_AfterTheLimit()
    {
        ChatState.ActivityRefresh = TimeSpan.FromMilliseconds(60);
        ChatState.ActivityLimit = TimeSpan.FromMilliseconds(200);
        await using var state = State(out var posts, Direct());
        await state.BeginActivityAsync(Thread, ChatActivity.File, 3);
        await WaitAsync(() => state.OutgoingActivity is null);
        Assert.That(posts[^1], Is.EqualTo(new ThreadAction(Thread, "typing", false, ChatActivity.File, 3)));
    }

    [Test]
    public async Task Sent_Never_WhenTypingIsOffOrOffline()
    {
        var chat = Direct(); chat.Features &= ~(int)ChatFeature.Typing;
        await using var state = State(out var posts, chat);
        await state.BeginActivityAsync(Thread, ChatActivity.Photo);
        await state.EndActivityAsync(Thread);
        await state.PublishTypingAsync(Thread, true);
        Assert.That(posts, Is.Empty, "Typing off: neither typing nor attachments leave the device.");
        chat.Features |= (int)ChatFeature.Typing;
        await state.ConnectivityChanged(false);
        await state.BeginActivityAsync(Thread, ChatActivity.Photo);
        Assert.That(posts, Is.Empty);
    }

    [Test]
    public async Task Sent_EndsWhenTheAttachmentIsDelivered()
    {
        await using var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "You");
        var posts = new List<ThreadAction>(); var attached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(true);
        var chat = Direct();
        using var http = new HttpClient(new Handler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/session") return Json(new SessionResponse(user, "token"));
            if (path.EndsWith("initialize")) return Json(new ChatDefaults(Guid.NewGuid(), []));
            if (path.EndsWith("thread-actions")) { var action = (await request.Content!.ReadFromJsonAsync<ThreadAction>())!; lock (posts) posts.Add(action); return new(HttpStatusCode.NoContent); }
            if (request.Method == HttpMethod.Post && path == "/api/chat/messages")
                return Json(new MessageReceipt((await request.Content!.ReadFromJsonAsync<SendMessage>())!.Id));
            if (path.EndsWith("/complete")) return Json(new StoredFile(Guid.NewGuid()));
            if (path.EndsWith("/attachments")) { attached.TrySetResult(); return new(HttpStatusCode.NoContent); }
            if (path.EndsWith("/deleted")) return Json(new ChatPage<Guid>([], 0));
            if (path.EndsWith("/messages")) return Json(new ChatPage<ChatMessage>([], 0));
            if (path.EndsWith(Thread.ToString())) return Json(chat);
            return Json(new ChatPage<Conversation>([chat], 1));
        })) { BaseAddress = new("https://yap.test/") };
        await using var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync(); await state.SynchronizeAsync(); await state.SelectAsync(Thread);
        var file = new ChatState.PickedFile(Guid.NewGuid().ToString("N"), "holiday.jpg", "image/jpeg", 1234, Staged: false, UploadId: Guid.NewGuid());
        await state.BeginActivityAsync(Thread, ChatState.ActivityFor(file.ContentType));
        await state.SendAsync(Thread, "", null, "main", file);
        await attached.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitAsync(() => state.OutgoingActivity is null);
        List<ThreadAction> sent; lock (posts) sent = posts.ToList();
        Assert.That(sent.First(), Is.EqualTo(new ThreadAction(Thread, "typing", true, ChatActivity.Photo, 1)));
        Assert.That(sent.Last(), Is.EqualTo(new ThreadAction(Thread, "typing", false, ChatActivity.Photo, 1)));
        Assert.That(JsonSerializer.Serialize(sent), Does.Not.Contain("holiday").And.Not.Contain("1234"), "Never the name or size.");
    }

    private static Conversation Direct() => new() { Id = Thread, Name = "Sam Reyes", Members = 2, PeerId = Sam };

    private static ChatState State(out List<ThreadAction> posts, Conversation selected)
    {
        var sent = posts = [];
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(true);
        var http = new HttpClient(new Handler(async request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("thread-actions"))
            { var action = (await request.Content!.ReadFromJsonAsync<ThreadAction>())!; lock (sent) sent.Add(action); }
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        })) { BaseAddress = new("https://yap.test/") };
        var state = new ChatState(null!, new ChatApi(http), js.Object);
        typeof(ChatState).GetProperty(nameof(ChatState.User))!.SetValue(state, new UserSession(Guid.NewGuid(), Guid.NewGuid(), "You"));
        typeof(ChatState).GetProperty(nameof(ChatState.Selected))!.SetValue(state, selected);
        state.Conversations.Add(selected);
        return state;
    }

    private static async Task WaitAsync(Func<bool> done)
    {
        for (var i = 0; i < 200 && !done(); i++) await Task.Delay(20);
        Assert.That(done(), Is.True, "Timed out waiting.");
    }

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => handle(request);
    }
}
