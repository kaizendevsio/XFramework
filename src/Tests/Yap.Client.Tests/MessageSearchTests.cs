using System.Text.Json;
using Microsoft.JSInterop;
using Moq;
using NUnit.Framework;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Client.Tests;

/// <summary>The cached scan is what a normal, end-to-end-encrypted account actually searches,
/// so it has to carry everything a result row shows.</summary>
public sealed class MessageSearchTests
{
    private static ChatMessage Message(Guid thread, string sender, bool mine, string text, int minutesAgo) => new()
    {
        Id = Guid.NewGuid(), ThreadId = thread, Sender = sender, Mine = mine, Text = text,
        CreatedAt = DateTime.UtcNow.AddMinutes(-minutesAgo), AvatarUrl = mine ? null : "https://yap.test/a.png"
    };

    private static async Task<(ChatState State, StoreFixture Fixture)> OfflineAsync(Guid thread, params ChatMessage[] messages)
    {
        var fixture = await StoreFixture.CreateAsync();
        var user = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Jamie");
        var scope = OfflineStore.Scope(user);
        await fixture.Store.SetSettingAsync("user", JsonSerializer.Serialize(user));
        await fixture.Store.SaveConversationsAsync(scope, [new Conversation { Id = thread, Name = "Sarah Mensah" }]);
        await fixture.Store.SaveMessagesAsync(scope, messages);
        var js = new Mock<IJSRuntime>();
        // Offline takes the same cached path an encrypted account takes while online.
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(false);
        using var http = new HttpClient(new Offline()) { BaseAddress = new("https://yap.test/") };
        var state = new ChatState(fixture.Store, new ChatApi(http), js.Object);
        await state.InitializeAsync();
        return (state, fixture);
    }

    [Test]
    public async Task SearchAsync_CachedScan_ReportsThatOnlySavedHistoryIsSearched()
    {
        var thread = Guid.NewGuid();
        var (state, fixture) = await OfflineAsync(thread, Message(thread, "Sarah Mensah", false, "anything", 1));
        await using var _ = fixture;
        await using var __ = state;
        Assert.That(state.SearchesCachedHistoryOnly, Is.True);
    }

    [Test]
    public async Task SearchAsync_CachedScan_CarriesSenderAndAvatarForEachHit()
    {
        var thread = Guid.NewGuid();
        var (state, fixture) = await OfflineAsync(thread,
            Message(thread, "Sarah Mensah", false, "I pushed the onboarding screens last night.", 30),
            Message(thread, "You", true, "Opened the onboarding flow — much better.", 10),
            Message(thread, "Sarah Mensah", false, "Unrelated chatter", 5));
        await using var _ = fixture;
        await using var __ = state;

        var hits = await state.SearchAsync("onboarding", thread);

        Assert.That(hits, Has.Count.EqualTo(2));
        // Newest first, matching the cap the cached scan applies.
        Assert.That(hits[0].Sender, Is.EqualTo("You"));
        Assert.That(hits[0].Mine, Is.True);
        Assert.That(hits[1].Sender, Is.EqualTo("Sarah Mensah"));
        Assert.That(hits[1].Mine, Is.False);
        Assert.That(hits[1].AvatarUrl, Is.EqualTo("https://yap.test/a.png"));
    }

    [Test]
    public async Task SearchAsync_StillIgnoresQueriesShorterThanTwoCharacters()
    {
        var thread = Guid.NewGuid();
        var (state, fixture) = await OfflineAsync(thread, Message(thread, "Sarah Mensah", false, "o o o", 1));
        await using var _ = fixture;
        await using var __ = state;
        Assert.That(await state.SearchAsync("o", thread), Is.Empty);
    }

    [Test]
    public async Task SearchAsync_AbandonsTheScanWhenTheKeystrokeIsSuperseded()
    {
        var thread = Guid.NewGuid();
        var (state, fixture) = await OfflineAsync(thread, Message(thread, "Sarah Mensah", false, "onboarding", 1));
        await using var _ = fixture;
        await using var __ = state;
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        Assert.ThrowsAsync<OperationCanceledException>(() => state.SearchAsync("onboarding", thread, cancelled.Token));
    }

    private sealed class Offline : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("offline");
    }
}
