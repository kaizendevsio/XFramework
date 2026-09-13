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

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider provider;
        private readonly HttpClient http;
        private readonly UserSession user = new(Guid.NewGuid(), Guid.NewGuid(), "Recipient");
        public ChatApi Api { get; }
        public ChatState Chat { get; }
        public VoiceState Voice { get; }
        public Fixture(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? respond = null, IJSRuntime? js = null)
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
            services.AddLogging(); services.AddSingleton(js); services.AddBoltMediaBrowser();
            provider = services.BuildServiceProvider();
            Voice = new VoiceState(Chat, Api, provider.GetRequiredService<IServiceScopeFactory>(), new Navigation(), NullLoggerFactory.Instance);
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
