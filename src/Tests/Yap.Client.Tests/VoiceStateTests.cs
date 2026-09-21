using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bolt.Media.Browser;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Moq;
using NUnit.Framework;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Client.Tests;

public sealed class VoiceStateTests
{
    [Test]
    public async Task SlowEncryptionInitialization_DoesNotConsumeTheSocketTicketLifetime()
    {
        var loading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<IJSObjectReference>(TaskCreationOptions.RunContinuationsAsynchronously);
        var module = new Mock<IJSObjectReference>();
        module.Setup(x => x.InvokeAsync<IJSObjectReference>("createAudioPipeline", It.IsAny<object?[]?>())).ReturnsAsync(module.Object);
        module.Setup(x => x.InvokeAsync<VoiceCapabilities>("checkVoiceCapabilities", It.IsAny<object?[]?>())).ReturnsAsync(new VoiceCapabilities(true, null, true));
        module.Setup(x => x.InvokeAsync<IJSObjectReference>("createSession", It.IsAny<object?[]?>()))
            .Returns(() => { loading.TrySetResult(); return new ValueTask<IJSObjectReference>(release.Task); });
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]?>())).ReturnsAsync(module.Object);
        var tickets = 0;
        Fixture? fixture = null;
        await using var owned = fixture = new Fixture((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/config")) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = JsonContent.Create(new { enabled = true, groupCalls = true, securityMode = "EndToEndEncrypted" }) });
            if (request.RequestUri.AbsolutePath.EndsWith("/connect")) tickets++;
            if (request.RequestUri.AbsolutePath == "/api/chat/calls/groups") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = JsonContent.Create(fixture!.GroupEvent(fixture.Invite()).Group) });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }, js.Object, encrypted: true);
        await fixture.Voice.InitializeAsync();
        var starting = fixture.Voice.StartAsync(Guid.NewGuid(), new Person(Guid.NewGuid(), "Friend", "friend"));
        Task ending = Task.CompletedTask;
        try
        {
            await loading.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.That(tickets, Is.Zero, "slow WASM initialization must finish before asking for a short-lived ticket");
            ending = fixture.Voice.EndAsync();
            Assert.That(fixture.Voice.IsCalling, Is.False);
        }
        finally { release.TrySetResult(module.Object); }
        await ending.WaitAsync(TimeSpan.FromSeconds(2));
        await starting.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(tickets, Is.Zero, "cancellation during initialization must not issue a ticket afterward");
    }

    [Test]
    public async Task RuntimeFailure_DoesNotExposeConnectionTicketToUser()
    {
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]?>()))
            .ThrowsAsync(new InvalidOperationException("All transports failed for wss://yap.test/socket?ticket=private-ticket"));
        await using var fixture = new Fixture((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = JsonContent.Create(new { enabled = true, groupCalls = true, securityMode = "EndToEndEncrypted" }) }), js.Object);
        await fixture.Voice.InitializeAsync();
        await fixture.Voice.StartAsync(Guid.NewGuid(), new Person(Guid.NewGuid(), "Friend", "friend"));
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Voice.IsCalling, Is.False);
            Assert.That(fixture.Voice.Error, Is.EqualTo("The call could not connect. Check your connection and microphone access."));
        });
    }

    [Test]
    public async Task Decline_DoesNotWaitForNetwork_AndReplayDoesNotReopenCall()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var fixture = new Fixture(async (_, ct) =>
        { await release.Task.WaitAsync(ct); return new(HttpStatusCode.NoContent); });
        var invite = fixture.Invite();
        await fixture.DeliverAsync(fixture.GroupEvent(invite));
        Assert.That(fixture.Voice.IsCalling, Is.True);
        await fixture.Voice.EndAsync().WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(fixture.Voice.IsCalling, Is.False);
        await fixture.DeliverAsync(fixture.GroupEvent(invite));
        await fixture.DeliverAsync(new("ready", invite, invite.RecipientId));
        Assert.That(fixture.Voice.IsCalling, Is.False);
        release.TrySetResult();
    }

    [Test]
    public async Task AccountChanged_EndsOldAccountCall()
    {
        await using var fixture = new Fixture();
        await fixture.DeliverAsync(fixture.GroupEvent(fixture.Invite()));
        fixture.Api.Account = "another-account";
        fixture.Chat.Notify();
        Assert.That(fixture.Voice.IsCalling, Is.False);
        Assert.That(fixture.Voice.Enabled, Is.False);
    }

    [Test]
    public async Task CancelWhilePreparing_LateCapabilityResultCannotCreateInvitation()
    {
        var capability = new TaskCompletionSource<VoiceCapabilities>(TaskCreationOptions.RunContinuationsAsynchronously);
        var checkedCapability = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var module = new Mock<IJSObjectReference>();
        module.Setup(x => x.InvokeAsync<VoiceCapabilities>("checkVoiceCapabilities", It.IsAny<object?[]?>()))
            .Returns(() => { checkedCapability.TrySetResult(); return new ValueTask<VoiceCapabilities>(capability.Task); });
        var js = new Mock<IJSRuntime>();
        js.Setup(x => x.InvokeAsync<IJSObjectReference>("import", It.IsAny<object?[]?>())).ReturnsAsync(module.Object);
        var invitations = 0;
        await using var fixture = new Fixture((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/config")) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { enabled = true, groupCalls = true, securityMode = "EndToEndEncrypted" }) });
            invitations++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }, js.Object);
        await fixture.Voice.InitializeAsync();
        var starting = fixture.Voice.StartAsync(Guid.NewGuid(), new Person(Guid.NewGuid(), "Friend", "friend"));
        await checkedCapability.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(fixture.Voice.IsCalling, Is.True);
        await fixture.Voice.EndAsync().WaitAsync(TimeSpan.FromSeconds(1));
        capability.TrySetResult(new(true, null, true));
        await starting.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Voice.IsCalling, Is.False);
            Assert.That(fixture.Voice.Invite, Is.Null);
            Assert.That(fixture.Voice.Error, Is.Null);
            Assert.That(invitations, Is.Zero);
        });
    }

    [Test]
    public async Task TrustedServerOnlyConfiguration_NeverEnablesPlaintextFallback()
    {
        await using var fixture = new Fixture((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = JsonContent.Create(new { enabled = true, groupCalls = false, securityMode = "TrustedServerTls" }) }));
        await fixture.Voice.InitializeAsync();
        await fixture.Voice.StartAsync(Guid.NewGuid(), new Person(Guid.NewGuid(), "Friend", "friend"));
        Assert.That(fixture.Voice.Enabled, Is.False);
        Assert.That(fixture.Voice.IsCalling, Is.False);
        await fixture.DeliverAsync(new("incoming", fixture.Invite()));
        Assert.That(fixture.Voice.IsCalling, Is.False);
    }

    [Test]
    public async Task EndedEventBeforeInvitation_DoesNotAllowLateIncomingReplay()
    {
        await using var fixture = new Fixture();
        var incoming = fixture.GroupEvent(fixture.Invite());
        await fixture.DeliverAsync(incoming with { Type = "group-ended" });
        await fixture.DeliverAsync(incoming);
        Assert.That(fixture.Voice.IsCalling, Is.False);
    }

    [Test]
    public async Task RemoteHangup_ClosesIncomingUi_AndIgnoresStaleReplay()
    {
        await using var fixture = new Fixture();
        var incoming = fixture.GroupEvent(fixture.Invite());
        await fixture.DeliverAsync(incoming);
        await fixture.DeliverAsync(incoming with { Type = "group-ended" });
        await fixture.DeliverAsync(incoming);
        Assert.That(fixture.Voice.IsCalling, Is.False);
        Assert.That(fixture.Voice.Incoming, Is.False);
    }

    [Test]
    public void RingMode_FollowsCallStateForEveryStatus()
    {
        Assert.Multiple(() =>
        {
            Assert.That(VoiceState.RingModeFor(true, true, "Incoming encrypted voice call"), Is.EqualTo("ringtone"));
            Assert.That(VoiceState.RingModeFor(true, false, "Ringing..."), Is.EqualTo("ringback"));
            Assert.That(VoiceState.RingModeFor(true, false, "Preparing microphone..."), Is.Empty);
            Assert.That(VoiceState.RingModeFor(true, false, "Securing call..."), Is.Empty);
            Assert.That(VoiceState.RingModeFor(true, false, "Connected"), Is.Empty);
            Assert.That(VoiceState.RingModeFor(false, false, "Ringing..."), Is.Empty);
            Assert.That(VoiceState.RingModeFor(false, true, "Incoming encrypted voice call"), Is.Empty);
        });
    }

    // The chosen ringtone belongs to the person being called. Whatever a caller's call is doing,
    // it must never ask for "ringtone" - that is how the picker leaked onto the calling side.
    [Test]
    public void Caller_IsNeverRungWithTheRingtoneThePersonalPickerSets()
    {
        string[] statuses = ["", "Preparing microphone...", VoiceState.RingingStatus, "Securing call...", "Connected", "Connecting..."];
        Assert.Multiple(() =>
        {
            Assert.That(statuses.Select(status => VoiceState.RingModeFor(true, false, status)), Has.None.EqualTo("ringtone"));
            Assert.That(statuses.Select(status => VoiceState.RingModeFor(true, true, status)), Has.All.EqualTo("ringtone"));
        });
    }

    // Autoplay policy answers a blocked resume() with a promise that never settles. The ring
    // queue chains every stop behind the start before it, so a wait with no end would leave the
    // ring unstoppable and the incoming screen without its retry button.
    [Test]
    public async Task AStartThatNeverAnswers_ReportsSilence_AndStillLetsTheRingStop()
    {
        var js = new RingJs { Hang = true };
        var previous = VoiceState.RingStartTimeout;
        VoiceState.RingStartTimeout = TimeSpan.FromMilliseconds(50);
        try
        {
            await using var fixture = new Fixture(js: js);
            await fixture.DeliverAsync(fixture.GroupEvent(fixture.Invite()));
            Assert.That(() => fixture.Voice.RingSilent, Is.True.After(2000, 20), "a ring nobody heard still has to offer the gesture that unblocks it");
            js.Hang = false;
            await fixture.Voice.EndAsync().WaitAsync(TimeSpan.FromSeconds(2));
            Assert.That(() => js.Snapshot().Last(), Is.EqualTo("yap.ring.stop").After(2000, 20));
        }
        finally { VoiceState.RingStartTimeout = previous; }
    }

    [Test]
    public async Task IncomingCall_RingsOnce_AndStopsWhenDeclined()
    {
        var js = new RingJs();
        await using var fixture = new Fixture(js: js);
        var invite = fixture.Invite();
        await fixture.DeliverAsync(fixture.GroupEvent(invite));
        Assert.That(js.Calls, Is.EqualTo(new[] { "yap.ring.start:ringtone" }));
        // A repeated invitation for the same call must not stack a second ring.
        await fixture.DeliverAsync(fixture.GroupEvent(invite));
        Assert.That(js.Calls, Has.Count.EqualTo(1));
        await fixture.Voice.EndAsync().WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(js.Calls, Is.EqualTo(new[] { "yap.ring.start:ringtone", "yap.ring.stop" }));
    }

    [Test]
    public async Task IncomingCall_StopsRingingOnAcceptAndOnRemoteHangup()
    {
        var accepted = new RingJs();
        await using (var fixture = new Fixture(js: accepted))
        {
            await fixture.DeliverAsync(fixture.GroupEvent(fixture.Invite()));
            await fixture.Voice.AcceptAsync();
            Assert.That(accepted.Calls.Last(), Is.EqualTo("yap.ring.stop"));
        }
        var hungUp = new RingJs();
        await using var remote = new Fixture(js: hungUp);
        var incoming = remote.GroupEvent(remote.Invite());
        await remote.DeliverAsync(incoming);
        await remote.DeliverAsync(incoming with { Type = "group-ended" });
        Assert.That(hungUp.Calls, Is.EqualTo(new[] { "yap.ring.start:ringtone", "yap.ring.stop" }));
    }

    [Test]
    public async Task IncomingCall_StopsRingingWhenTheAccountChangesOrTheStateIsDisposed()
    {
        var switched = new RingJs();
        await using (var fixture = new Fixture(js: switched))
        {
            await fixture.DeliverAsync(fixture.GroupEvent(fixture.Invite()));
            fixture.Api.Account = "another-account";
            fixture.Chat.Notify();
            Assert.That(switched.Calls.Last(), Is.EqualTo("yap.ring.stop"));
        }
        var closed = new RingJs();
        var disposing = new Fixture(js: closed);
        await disposing.DeliverAsync(disposing.GroupEvent(disposing.Invite()));
        await disposing.DisposeAsync();
        Assert.That(closed.Calls.Last(), Is.EqualTo("yap.ring.stop"));
    }

    [Test]
    public async Task BlockedAutoplay_OffersTheGestureThatRetriesTheRing()
    {
        var js = new RingJs { Audible = false };
        await using var fixture = new Fixture(js: js);
        await fixture.DeliverAsync(fixture.GroupEvent(fixture.Invite()));
        Assert.That(fixture.Voice.RingSilent, Is.True);
        js.Audible = true;
        Assert.That(await fixture.Voice.RetryRingAsync(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Voice.RingSilent, Is.False);
            Assert.That(js.Calls, Is.EqualTo(new[] { "yap.ring.start:ringtone", "yap.ring.unlock", "yap.ring.start:ringtone" }));
        });
        await fixture.Voice.EndAsync().WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(fixture.Voice.RingSilent, Is.False);
        Assert.That(await fixture.Voice.RetryRingAsync(), Is.False, "A ended call must not be able to restart its ring.");
    }

    // Records the ring calls the browser would have made; every other identifier answers with a default.
    private sealed class RingJs : IJSRuntime
    {
        public List<string> Calls { get; } = [];
        public bool Audible { get; set; } = true;
        /// <summary>Answers yap.ring.start the way a blocked autoplay policy does: never.</summary>
        public bool Hang { get; set; }
        public string[] Snapshot() { lock (Calls) return [.. Calls]; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => Record<TValue>(identifier, args, default);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => Record<TValue>(identifier, args, cancellationToken);
        private ValueTask<TValue> Record<TValue>(string identifier, object?[]? args, CancellationToken cancellationToken)
        {
            if (identifier.StartsWith("yap.ring.")) lock (Calls) Calls.Add(args is { Length: > 0 } ? $"{identifier}:{args[0]}" : identifier);
            if (Hang && identifier == "yap.ring.start")
            {
                var pending = new TaskCompletionSource<TValue>(TaskCreationOptions.RunContinuationsAsynchronously);
                cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken));
                return new(pending.Task);
            }
            object? value = typeof(TValue) == typeof(RingStatus) ? new RingStatus(Audible, false)
                : typeof(TValue) == typeof(bool) ? (object)true
                : typeof(TValue).IsValueType ? Activator.CreateInstance(typeof(TValue)) : null;
            return new((TValue)value!);
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider provider;
        private readonly HttpClient http;
        private readonly UserSession user = new(Guid.NewGuid(), Guid.NewGuid(), "Recipient");
        public ChatApi Api { get; }
        public ChatState Chat { get; }
        public VoiceState Voice { get; }
        public Fixture(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? respond = null, IJSRuntime? js = null, bool encrypted = false)
        {
            js ??= Mock.Of<IJSRuntime>();
            http = new HttpClient(new Handler(respond ?? ((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)))))
            { BaseAddress = new("https://yap.test/") };
            Api = new ChatApi(http) { Account = OfflineStore.Scope(user) };
            Chat = new ChatState(null!, Api, js);
            typeof(ChatState).GetProperty(nameof(ChatState.User))!.SetValue(Chat, user);
            Chat.Encryption.Status.Approved = true;
            Chat.Encryption.Status.DeviceId = Guid.NewGuid();
            var services = new ServiceCollection();
            services.AddLogging(); services.AddSingleton(js); services.AddBoltMediaBrowser(options =>
            { if (encrypted) options.SecurityMode = MediaSecurityMode.AuthenticatedSFrame; });
            provider = services.BuildServiceProvider();
            Voice = new VoiceState(Chat, Api, provider.GetRequiredService<IServiceScopeFactory>(), new Navigation(), NullLoggerFactory.Instance, js);
        }
        public YapCallInvite Invite() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Caller", user.CredentialId, DateTimeOffset.UtcNow.AddMinutes(1));
        public YapCallEvent GroupEvent(YapCallInvite invite) => new("group-incoming", invite, Group: new(invite.Id, invite.ThreadId,
            invite.CallerId, invite.CallerName, 1, invite.ExpiresAt,
            [new(invite.CallerId, Guid.NewGuid(), true, false, false, false), new(user.CredentialId, Guid.Empty, false, false, false, false)]));
        public Task DeliverAsync(YapCallEvent value) => Chat.VoiceEvent(JsonSerializer.Serialize(value));
        public async ValueTask DisposeAsync()
        { await Voice.DisposeAsync(); await Chat.DisposeAsync(); await provider.DisposeAsync(); http.Dispose(); }
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request, cancellationToken); }
    private sealed class Navigation : NavigationManager
    { public Navigation() => Initialize("https://yap.test/", "https://yap.test/"); }
}
