using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using IdentityServer.Integration.Drivers;
using MemoryPack;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace XFramework.Core.Tests.DataContext;

public sealed class GeneratedQueryTokenScopeTests
{
    [TestCase(true, false, false)]
    [TestCase(true, false, true)]
    [TestCase(false, false, false)]
    [TestCase(false, false, true)]
    [TestCase(true, true, false)]
    [TestCase(true, true, true)]
    [TestCase(false, true, false)]
    [TestCase(false, true, true)]
    public async Task GeneratedWrapper_QueryAndStream_RequestOnlyRequiredScopes(bool hasActor, bool bypass, bool stream)
    {
        var actor = new ActorProvider(hasActor ? "test-actor" : null);
        var tokens = new TokenProvider { StopAtTokenRequest = true };
        // Instantiate the actual compiled generated IdentityServer wrapper. Unused CRUD
        // services and transport are null: the capture stops before any network operation.
        var constructor = typeof(IdentityServerServiceWrapper).GetConstructors()
            .Single(x => x.GetParameters().Any(p => p.ParameterType == typeof(IServiceTokenProvider)));
        var arguments = constructor.GetParameters().Select(parameter =>
            parameter.ParameterType == typeof(IServiceTokenProvider) ? (object)tokens :
            parameter.ParameterType == typeof(IActorAccessTokenProvider) ? actor : null).ToArray();
        var wrapper = (IDataContextServiceWrapper)constructor.Invoke(arguments);
        var bytes = MemoryPackSerializer.Serialize(new QueryDescriptor
        {
            EntityTypeName = "IdentityCredential", IgnoreQueryFilters = bypass,
            Metadata = new RequestMetadata { RequestedTenantId = Guid.NewGuid() }
        });
        Func<Task> call = async () =>
        {
            if (stream)
            {
                await foreach (var _ in wrapper.ExecuteQueryStreamAsync(bytes)) { }
            }
            else
                await wrapper.ExecuteQueryAsync(bytes);
        };
        await call.Should().ThrowAsync<TokenRequestCapturedException>();
        tokens.Audience.Should().Be(XFrameworkServiceNames.IdentityServer);
        var expected = new List<string> { XFrameworkServiceScopes.DataContextQuery };
        if (bypass) expected.Add(XFrameworkServiceScopes.DataContextQueryAllTenants);
        if (bypass || !hasActor) expected.Add(XFrameworkServiceScopes.TenantTarget);
        tokens.Scopes.Should().BeEquivalentTo(expected);
        actor.Calls.Should().Be(1);
    }

    [Test]
    public async Task QueryEnvelope_ScopeSelectionAndPayloadUseTheSameActor()
    {
        var actor = new ActorProvider("test-actor");
        var tokens = new TokenProvider();
        var tenant = Guid.NewGuid();
        var bytes = await BoltInvocationEnvelopeFactory.CreateDataContextQueryAsync(
            new QueryDescriptor { Metadata = new RequestMetadata { RequestedTenantId = tenant } },
            XFrameworkServiceNames.IdentityServer, tokens, actor);
        var envelope = MemoryPackSerializer.Deserialize<BoltInvocationEnvelope>(bytes)!;
        envelope.ActorAccessToken.Should().Be("test-actor");
        envelope.ServiceAccessToken.Should().Be("test-service");
        MemoryPackSerializer.Deserialize<QueryDescriptor>(envelope.Payload)!.Metadata.RequestedTenantId.Should().Be(tenant);
        tokens.Scopes.Should().Equal(XFrameworkServiceScopes.DataContextQuery);
        actor.Calls.Should().Be(1);
    }

    private sealed class ActorProvider(string? token) : IActorAccessTokenProvider
    {
        public int Calls { get; private set; }
        public ValueTask<string?> GetTokenAsync(CancellationToken ct = default)
        {
            Calls++;
            return ValueTask.FromResult(token);
        }
    }

    private sealed class TokenProvider : IServiceTokenProvider
    {
        public bool StopAtTokenRequest { get; init; }
        public string? Audience { get; private set; }
        public IReadOnlyCollection<string>? Scopes { get; private set; }
        public ValueTask<string> GetTokenAsync(string audience, IReadOnlyCollection<string>? scopes = null, CancellationToken ct = default)
        {
            Audience = audience;
            Scopes = scopes?.ToArray();
            if (StopAtTokenRequest) throw new TokenRequestCapturedException();
            return ValueTask.FromResult("test-service");
        }
    }

    private sealed class TokenRequestCapturedException : Exception;
}
