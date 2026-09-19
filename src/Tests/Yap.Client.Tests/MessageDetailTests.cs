using System.Net;
using System.Net.Http.Json;
using Microsoft.JSInterop;
using Moq;
using NUnit.Framework;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Client.Tests;

/// <summary>The two per-message details: who reacted, and when the caller's own message arrived.
/// Neither is allowed to leak into the message page, and neither may format a time in .NET.</summary>
public sealed class MessageDetailTests
{
    private static readonly Person Sarah = new(Guid.NewGuid(), "Sarah Mensah", "sarah");
    private static readonly Person Robin = new(Guid.NewGuid(), "Robin Chen", "robin");
    private static readonly Person Amara = new(Guid.NewGuid(), "Amara Okoye", "amara");
    private static readonly Guid Self = Guid.NewGuid();
    private static readonly DateTime At = new(2026, 3, 4, 9, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Status_WithoutDetail_AddsNothingToTheSentLine()
    {
        Assert.That(MessageReceipts.Status(null, false, [Sarah], Self), Is.Empty);
        Assert.That(MessageReceipts.Status(null, true, [Sarah, Robin], Self), Is.Empty);
    }

    [Test]
    public void Status_DirectChat_ReadsAsDeliveredThenRead()
    {
        var detail = new MessageReceiptDetail(Guid.NewGuid(), true,
            [new(Sarah, At.AddSeconds(20), At.AddMinutes(3))]);
        var rows = MessageReceipts.Status(detail, false, [Sarah], Self);
        Assert.That(rows.Select(x => x.Who), Is.EqualTo(new[] { "Delivered", "Read" }));
        Assert.That(rows[0].At, Is.EqualTo(At.AddSeconds(20)));
        Assert.That(rows[1].At, Is.EqualTo(At.AddMinutes(3)));
        Assert.That(rows[1].Read, Is.True);
    }

    [Test]
    public void Status_DirectChat_UndeliveredSaysSo_AndNeverInventsARead()
    {
        var rows = MessageReceipts.Status(new(Guid.NewGuid(), true, []), false, [Sarah], Self);
        Assert.That(rows.Single().Who, Is.EqualTo("Delivered"));
        Assert.That(rows.Single().State, Is.EqualTo("Not yet"));
        Assert.That(rows.Single().At, Is.Null);
    }

    /// <summary>Feature off means no read line at all, not a read line that says "never".</summary>
    [Test]
    public void Status_WithReadReceiptsOff_ShowsDeliveryOnly()
    {
        var detail = new MessageReceiptDetail(Guid.NewGuid(), false,
            [new(Sarah, At.AddSeconds(20), At.AddMinutes(3))]);
        Assert.That(MessageReceipts.Status(detail, false, [Sarah], Self).Select(x => x.Who), Is.EqualTo(new[] { "Delivered" }));
        var group = MessageReceipts.Status(detail, true, [Sarah], Self);
        Assert.That(group.Single().State, Is.EqualTo("Delivered"));
        Assert.That(group.Single().Read, Is.False);
    }

    [Test]
    public void Status_Group_ListsReadersFirstThenDeliveredThenWhoeverItHasNotReached()
    {
        var detail = new MessageReceiptDetail(Guid.NewGuid(), true,
        [
            new(Robin, At.AddSeconds(10), null),
            new(Sarah, At.AddSeconds(30), At.AddMinutes(5))
        ]);
        var rows = MessageReceipts.Status(detail, true, [Sarah, Robin, Amara, new(Self, "You", "you")], Self);
        Assert.That(rows.Select(x => x.Who), Is.EqualTo(new[] { "Sarah Mensah", "Robin Chen", "Amara Okoye" }),
            "The caller is not a recipient of their own message");
        Assert.That(rows.Select(x => x.State), Is.EqualTo(new[] { "Read", "Delivered", "Not delivered" }));
        Assert.That(rows[2].At, Is.Null);
        Assert.That(rows.Select(x => x.Key).Distinct().Count(), Is.EqualTo(3));
    }

    [Test]
    public async Task ReceiptsAsync_NeverAsksAboutAMessageTheCallerDidNotSend()
    {
        var calls = new List<string>();
        await using var state = await StateAsync(calls);
        var theirs = new ChatMessage { Id = Guid.NewGuid(), ThreadId = Guid.NewGuid(), Mine = false };
        Assert.That(await state.ReceiptsAsync(theirs), Is.Null);
        Assert.That(calls.Any(path => path.EndsWith("/receipts", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public async Task DetailsAreFetchedPerMessage_FromTheConversationScopedRoute()
    {
        var calls = new List<string>();
        var thread = Guid.NewGuid(); var id = Guid.NewGuid();
        await using var state = await StateAsync(calls);
        var mine = new ChatMessage { Id = id, ThreadId = thread, Mine = true };
        var reactors = await state.ReactorsAsync(mine);
        var receipts = await state.ReceiptsAsync(mine);
        Assert.That(reactors.Single().Person.Name, Is.EqualTo("Sarah Mensah"));
        Assert.That(receipts!.Entries.Single().ReadAt, Is.EqualTo(At.AddMinutes(3)));
        Assert.That(calls, Is.EqualTo(new[]
        {
            $"/api/chat/conversations/{thread}/messages/{id}/reactions",
            $"/api/chat/conversations/{thread}/messages/{id}/receipts"
        }));
    }

    private static async Task<ChatState> StateAsync(List<string> calls)
    {
        var fixture = await StoreFixture.CreateAsync();
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(true);
        var http = new HttpClient(new Handler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/session") return Json(new SessionResponse(new UserSession(Self, Guid.NewGuid(), "You"), "token"));
            if (path.EndsWith("initialize", StringComparison.Ordinal)) return Json(new ChatDefaults(Guid.NewGuid(), []));
            if (path.EndsWith("/reactions", StringComparison.Ordinal))
            { calls.Add(path); return Json(new List<MessageReactor> { new(Guid.NewGuid(), "❤️", Sarah, At, false) }); }
            if (path.EndsWith("/receipts", StringComparison.Ordinal))
            {
                calls.Add(path);
                return Json(new MessageReceiptDetail(Guid.NewGuid(), true, [new(Sarah, At.AddSeconds(20), At.AddMinutes(3))]));
            }
            return Json(new ChatPage<Conversation>([], 0));
        })) { BaseAddress = new("https://yap.test/") };
        var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync();
        calls.Clear();
        return state;
    }

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(handle(request));
    }
}
