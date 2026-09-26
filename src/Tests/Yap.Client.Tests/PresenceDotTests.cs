using Microsoft.JSInterop;
using Moq;
using NUnit.Framework;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Client.Tests;

/// <summary>The avatar dot: green while "Active now", orange for a recent "Last seen", otherwise none.</summary>
public sealed class PresenceDotTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

    [TestCase(30, null, PresenceStatus.Active)]      // heartbeat still inside its 45 s lifetime
    [TestCase(-1, 46.0 / 60, PresenceStatus.Away)]   // just lapsed
    [TestCase(null, 14.9, PresenceStatus.Away)]
    [TestCase(null, 15.0, PresenceStatus.Offline)]   // AwayWindow is exclusive
    [TestCase(null, 90.0, PresenceStatus.Offline)]
    [TestCase(null, null, PresenceStatus.Offline)]   // no heartbeat, or a hidden status
    public void Thresholds(int? activeForSeconds, double? lastSeenMinutesAgo, PresenceStatus expected) =>
        Assert.That(PresenceDot.Of(activeForSeconds is { } s ? Now.AddSeconds(s) : null,
            lastSeenMinutesAgo is { } m ? Now.AddMinutes(-m) : null, Now), Is.EqualTo(expected));

    [Test]
    public void Labels_AreWhatAScreenReaderHears()
    {
        Assert.That(PresenceDot.Label(PresenceStatus.Active), Is.EqualTo("Active now"));
        Assert.That(PresenceDot.Label(PresenceStatus.Away), Is.EqualTo("Away"));
        Assert.That(PresenceDot.Label(PresenceStatus.Offline), Is.Null);
        Assert.That(PresenceDot.CssClass(PresenceStatus.Offline), Is.Null, "Offline draws no dot at all.");
    }

    [Test]
    public async Task NoDot_ForYourself_Groups_HiddenStatus_OrWhileOffline()
    {
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<bool>("yap.device.online", It.IsAny<object?[]?>())).ReturnsAsync(false);
        await using var state = new ChatState(null!, new ChatApi(new HttpClient { BaseAddress = new("https://yap.test/") }), js.Object);
        var me = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "You");
        typeof(ChatState).GetProperty(nameof(ChatState.User))!.SetValue(state, me);
        var peer = Guid.NewGuid(); var live = DateTime.UtcNow.AddSeconds(30);
        var direct = new Conversation { Id = Guid.NewGuid(), PeerId = peer, PeerActiveUntil = live, PeerLastActiveAt = DateTime.UtcNow };
        var away = new Conversation { Id = Guid.NewGuid(), PeerId = Guid.NewGuid(), PeerLastActiveAt = DateTime.UtcNow.AddMinutes(-5) };
        var hidden = new Conversation { Id = Guid.NewGuid(), PeerId = Guid.NewGuid() }; // the host sends no heartbeat for a hidden status
        var group = new Conversation { Id = Guid.NewGuid(), Group = true, PeerActiveUntil = live, PeerId = peer };
        state.Conversations.AddRange([direct, away, hidden, group]);

        Assert.That(state.PresenceOf(direct), Is.EqualTo(PresenceStatus.Active));
        Assert.That(state.PresenceOf(away), Is.EqualTo(PresenceStatus.Away));
        Assert.That(state.PresenceOf(hidden), Is.EqualTo(PresenceStatus.Offline));
        Assert.That(state.PresenceOf(group), Is.EqualTo(PresenceStatus.Offline), "Groups have no presence of their own.");
        Assert.That(state.PresenceOf(new Person(me.CredentialId, "You", "you", ActiveUntil: live)), Is.EqualTo(PresenceStatus.Offline), "Never on your own avatar.");
        Assert.That(state.PresenceOf(new Person(peer, "Sam", "sam", ActiveUntil: live)), Is.EqualTo(PresenceStatus.Active));
        Assert.That(state.PresenceOfConversation(direct.Id), Is.EqualTo(PresenceStatus.Active), "A call row finds its conversation.");
        Assert.That(state.PresenceOfPerson(peer), Is.EqualTo(PresenceStatus.Active), "A picker finds the direct chat with that person.");
        Assert.That(state.PresenceOfPerson(Guid.NewGuid()), Is.EqualTo(PresenceStatus.Offline), "No direct chat, no shared status, no dot.");

        await state.ConnectivityChanged(false);
        Assert.That(state.PresenceOf(direct), Is.EqualTo(PresenceStatus.Offline), "A cached heartbeat means nothing while this device is offline.");
    }
}
