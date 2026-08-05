using System.Net;
using System.Reflection;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Domain.Shared.Contracts.Responses;
using Communications.Integration.Clients;
using Communications.Integration.Drivers;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Security;

namespace Communications.Tests.Integration;

public sealed class CommunicationsChatClientTests
{
    [Test]
    public async Task CurrentActorSession_PropagatesActorTokenTenantAndCancellation()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        const string actorToken = "actor-access-token";
        using var cancellation = new CancellationTokenSource();
        var actorProvider = new StubActorProvider(new CommunicationsChatActor(
            tenantId,
            credentialId,
            "device-1",
            actorToken));
        var tokenScope = new RecordingActorAccessTokenScope();
        var proxy = DispatchProxy.Create<ICommunicationsServiceWrapper, RecordingWrapperProxy>();
        var recordingProxy = (RecordingWrapperProxy)(object)proxy;
        recordingProxy.OnInvoke = (method, arguments) =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(method.Name, Is.EqualTo(nameof(ICommunicationsServiceWrapper.GetUnreadCountsAsync)));
                Assert.That(tokenScope.CurrentToken, Is.EqualTo(actorToken));
                Assert.That(arguments[0], Is.TypeOf<GetUnreadCountsRequest>());
                Assert.That(((GetUnreadCountsRequest)arguments[0]!).Metadata!.RequestedTenantId, Is.EqualTo(tenantId));
                Assert.That((CancellationToken)arguments[1]!, Is.EqualTo(cancellation.Token));
            });

            return Task.FromResult(new QueryResponse<GetUnreadCountsResponse>
            {
                HttpStatusCode = HttpStatusCode.OK
            });
        };
        var client = new CommunicationsChatClient(
            proxy,
            new ConfigurationBuilder().Build(),
            actorProvider,
            tokenScope);

        var session = await client.ForCurrentActorAsync(ct: cancellation.Token);
        var response = await session.GetUnreadCountsAsync(cancellation.Token);

        Assert.Multiple(() =>
        {
            Assert.That(response.HttpStatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(session.TenantId, Is.EqualTo(tenantId));
            Assert.That(session.CredentialId, Is.EqualTo(credentialId));
            Assert.That(session.DeviceId, Is.EqualTo("device-1"));
            Assert.That(tokenScope.CurrentToken, Is.Null);
            Assert.That(actorProvider.CallCount, Is.EqualTo(2));
        });
    }

    private sealed class StubActorProvider(CommunicationsChatActor actor) : ICommunicationsChatActorProvider
    {
        public int CallCount { get; private set; }

        public ValueTask<CommunicationsChatActor?> GetCurrentActorAsync(CancellationToken ct = default)
        {
            CallCount++;
            return ValueTask.FromResult<CommunicationsChatActor?>(actor);
        }
    }

    private sealed class RecordingActorAccessTokenScope : IActorAccessTokenScope
    {
        public string? CurrentToken { get; private set; }

        public IDisposable Push(string actorAccessToken)
        {
            var previous = CurrentToken;
            CurrentToken = actorAccessToken;
            return new CallbackDisposable(() => CurrentToken = previous);
        }
    }

    private sealed class CallbackDisposable(Action callback) : IDisposable
    {
        public void Dispose() => callback();
    }

    private class RecordingWrapperProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[], object?> OnInvoke { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            OnInvoke(targetMethod!, args ?? []);
    }
}
