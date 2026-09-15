using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using NUnit.Framework;
using Yap.Client.Services;
using Yap.Contracts;

namespace Yap.Client.Tests;

[TestFixture]
public sealed class RecipientRosterCacheTests
{
    [Test]
    public async Task ConcurrentSends_ShareVerifiedFetch_AndDoNotExposeCachedArray()
    {
        using var fixture = new Fixture();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.BeforeResponse = _ => release.Task;
        var sends = Enumerable.Range(0, 20).Select(_ => fixture.Send()).ToArray();
        Assert.That(fixture.Requests, Is.EqualTo(1));
        release.SetResult();
        await Task.WhenAll(sends);
        sends[0].Result[0] = default;
        var cached = await fixture.Send();
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Requests, Is.EqualTo(1));
            Assert.That(fixture.Js.Verifications, Is.EqualTo(2));
            Assert.That(cached[0].GetProperty("credentialId").GetGuid(), Is.EqualTo(fixture.User.CredentialId));
            Assert.That(fixture.Encryption.Status.Approved, Is.True);
        });
    }

    [Test]
    public async Task Cache_ExpiresAfterFifteenSeconds_AndRetainsAtMostEightThreads()
    {
        using var fixture = new Fixture();
        await fixture.Send();
        fixture.Clock.Advance(TimeSpan.FromSeconds(14));
        await fixture.Send();
        Assert.That(fixture.Requests, Is.EqualTo(1));
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        await fixture.Send();
        Assert.That(fixture.Requests, Is.EqualTo(2));
        for (var i = 0; i < 8; i++) await fixture.Send(Guid.NewGuid());
        await fixture.Send();
        Assert.That(fixture.Requests, Is.EqualTo(11), "The oldest thread was evicted by the ninth distinct thread.");
    }

    [Test]
    public async Task CallsAndAudienceRestrictedEdits_AlwaysFetchFresh_WithoutPoisoningSendCache()
    {
        using var fixture = new Fixture();
        await fixture.Send();
        await fixture.Encryption.RecipientsAsync(fixture.User, fixture.Thread);
        await fixture.Encryption.RecipientsAsync(fixture.User, fixture.Thread);
        var restricted = await fixture.Encryption.RecipientsAsync(fixture.User, fixture.Thread, true, [fixture.Peer]);
        await fixture.Encryption.RecipientsAsync(fixture.User, fixture.Thread, true, [fixture.Peer]);
        var full = await fixture.Send();
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Requests, Is.EqualTo(5));
            Assert.That(fixture.Js.Verifications, Is.EqualTo(8), "Historical edits verify only their restricted audience.");
            Assert.That(restricted.Select(x => x.GetProperty("credentialId").GetGuid()), Is.EqualTo(new[] { fixture.Peer }));
            Assert.That(full, Has.Length.EqualTo(2));
        });
    }

    [Test]
    public async Task FailedSignatureVerification_IsNeverCached()
    {
        using var fixture = new Fixture();
        fixture.Js.RejectNextDirectory = true;
        Assert.ThrowsAsync<JSException>(async () => await fixture.Send());
        await fixture.Send();
        await fixture.Send();
        Assert.That(fixture.Requests, Is.EqualTo(2));
    }

    [Test]
    public async Task ThreadInvalidation_PreservesOtherCompletedRosters_AndAllInvalidationClearsThem()
    {
        using var fixture = new Fixture();
        var other = Guid.NewGuid();
        await fixture.Send();
        await fixture.Send(other);
        fixture.Encryption.InvalidateRecipients(fixture.Thread);
        await fixture.Send(other);
        await fixture.Send();
        Assert.That(fixture.Requests, Is.EqualTo(3));
        fixture.Encryption.InvalidateRecipients();
        await fixture.Send(other);
        await fixture.Send();
        Assert.That(fixture.Requests, Is.EqualTo(5));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task InvalidationDuringFetch_DiscardsOldResponse_EvenIfPendingEntryWasEvicted(bool evict)
    {
        using var fixture = new Fixture();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.BeforeResponse = count => count == 1 ? release.Task : Task.CompletedTask;
        var pending = fixture.Send();
        if (evict) for (var i = 0; i < 8; i++) await fixture.Send(Guid.NewGuid());
        fixture.Encryption.InvalidateRecipients(fixture.Thread);
        release.SetResult();
        var result = await pending;
        Assert.That(result[0].GetProperty("revision").GetInt32(), Is.EqualTo(evict ? 10 : 2));
        Assert.That(fixture.Js.AcceptedRevisions, Does.Not.Contain(1));
    }

    [Test]
    public async Task AccountChange_DoesNotAcceptOldPendingDirectory_OrReusePreviousAccountCache()
    {
        using var fixture = new Fixture();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.BeforeResponse = count => count == 1 ? release.Task : Task.CompletedTask;
        var pending = fixture.Send();
        var next = new UserSession(Guid.NewGuid(), Guid.NewGuid(), "Next");
        fixture.Api.Account = OfflineStore.Scope(next);
        await fixture.Encryption.RecipientsAsync(next, fixture.Thread, true);
        release.SetResult();
        Assert.ThrowsAsync<OperationCanceledException>(async () => await pending);
        Assert.That(fixture.Js.AcceptedRevisions, Does.Not.Contain(1));
        fixture.Api.Account = OfflineStore.Scope(fixture.User);
        await fixture.Send();
        Assert.That(fixture.Requests, Is.EqualTo(3));
    }

    [Test]
    public async Task OwnKeyMutation_InvalidatesRecipientRoster()
    {
        using var fixture = new Fixture();
        await fixture.Send();
        await fixture.Encryption.ProposeAsync(fixture.User);
        await fixture.Send();
        Assert.That(fixture.Requests, Is.EqualTo(2));
    }

    private sealed class Fixture : IDisposable
    {
        public UserSession User { get; } = new(Guid.NewGuid(), Guid.NewGuid(), "Owner");
        public Guid Peer { get; } = Guid.NewGuid();
        public Guid Thread { get; } = Guid.NewGuid();
        public Clock Clock { get; } = new();
        public Runtime Js { get; } = new();
        public ChatApi Api { get; }
        public ChatEncryption Encryption { get; }
        public Func<int, Task>? BeforeResponse { get; set; }
        public int Requests;
        private readonly HttpClient http;
        public Fixture()
        {
            http = new(new Handler(async () =>
            {
                var count = Interlocked.Increment(ref Requests);
                if (BeforeResponse is not null) await BeforeResponse(count);
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new[]
                {
                    new { credentialId = User.CredentialId, revision = count },
                    new { credentialId = Peer, revision = count }
                }) };
            })) { BaseAddress = new("https://yap.test/") };
            Api = new(http) { Account = OfflineStore.Scope(User) };
            Encryption = new(Api, Js, Clock);
        }
        public Task<JsonElement[]> Send(Guid? thread = null) => Encryption.RecipientsAsync(User, thread ?? Thread, true);
        public void Dispose() => http.Dispose();
    }
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan duration) => now += duration;
    }
    private sealed class Runtime : IJSRuntime
    {
        public int Verifications;
        public bool RejectNextDirectory;
        public List<int> AcceptedRevisions { get; } = [];
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (identifier == "yap.encryption.acceptDirectory")
            {
                if (RejectNextDirectory) { RejectNextDirectory = false; throw new JSException("Invalid directory signature."); }
                Verifications++;
                AcceptedRevisions.Add(((JsonElement)args![1]!).GetProperty("revision").GetInt32());
            }
            if (identifier == "yap.encryption.status")
                return ValueTask.FromResult((TValue)(object)new ChatEncryption.EncryptionStatus { Approved = true });
            if (identifier == "yap.encryption.proposeDevice")
                return ValueTask.FromResult((TValue)(object)JsonSerializer.SerializeToElement(new { deviceId = Guid.NewGuid() }));
            return ValueTask.FromResult(default(TValue)!);
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
    private sealed class Handler(Func<Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send();
    }
}
