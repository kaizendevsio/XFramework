using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Bolt.Client;
using Bolt.Protocol;
using FluentAssertions;
using MemoryPack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using XFramework.Core.DataContext;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace XFramework.Core.Tests.DataContext;

[TestFixture]
public sealed class DataContextBoltHandlerTests
{
    [Test]
    public async Task QueryRoute_AuthorizedCaller_PassesAuthenticatedSenderContextAndExecutesQuery()
    {
        var metadata = CreateMetadata();
        var context = new BoltInboundRequestContext(Guid.NewGuid(), BoltCodec.Fnv1aHash("portal"));
        var cancellationToken = new CancellationTokenSource().Token;
        var expectedResponse = new byte[] { 1, 2, 3 };
        var (client, authorizer, queryService, handler) = CreateHandler("__db_query__");
        await using var disposableClient = client;

        authorizer
            .Setup(x => x.AuthorizeAsync(
                It.Is<InvocationCredentials>(value => HasExpectedCredentials(value)),
                It.Is<RequestMetadata>(value => HasSameMetadata(value, metadata)),
                context,
                It.Is<InvocationAuthorizationPolicy>(policy =>
                    HasPolicy(policy, XFrameworkServiceScopes.DataContextQuery)),
                cancellationToken))
            .ReturnsAsync(Authorized(metadata));
        queryService
            .Setup(x => x.ExecuteAsync(It.IsAny<byte[]>(), cancellationToken))
            .ReturnsAsync(expectedResponse);

        var requestPayload = MemoryPackSerializer.Serialize(new QueryDescriptor
        {
            EntityTypeName = "Tenant",
            Metadata = metadata
        });
        var payload = Envelope(requestPayload);
        var result = await handler(payload, context, cancellationToken);

        result.Item1.Should().Be(HttpStatusCode.OK);
        result.Item2.ToArray().Should().Equal(expectedResponse);
        authorizer.VerifyAll();
        queryService.Verify(x => x.ExecuteAsync(It.Is<byte[]>(bytes => bytes.SequenceEqual(requestPayload)), cancellationToken), Times.Once);
        queryService.Verify(x => x.ExecuteChangesAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task QueryRoute_IgnoreFilters_RequiresCrossTenantQueryScope()
    {
        var metadata = CreateMetadata();
        var context = new BoltInboundRequestContext(Guid.NewGuid(), BoltCodec.Fnv1aHash("portal"));
        var (client, authorizer, queryService, handler) = CreateHandler("__db_query__");
        await using var disposableClient = client;

        authorizer
            .Setup(x => x.AuthorizeAsync(
                It.Is<InvocationCredentials>(value => HasExpectedCredentials(value)),
                It.Is<RequestMetadata>(value => HasSameMetadata(value, metadata)),
                context,
                It.Is<InvocationAuthorizationPolicy>(policy => HasPolicy(
                    policy,
                    XFrameworkServiceScopes.DataContextQuery,
                    XFrameworkServiceScopes.DataContextQueryAllTenants)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TrustedInvocationResult.Failure("missing cross-tenant scope", 403));

        var requestPayload = MemoryPackSerializer.Serialize(new QueryDescriptor
        {
            EntityTypeName = "Tenant",
            IgnoreQueryFilters = true,
            Metadata = metadata
        });
        var payload = Envelope(requestPayload);
        var result = await handler(payload, context, CancellationToken.None);

        result.Item1.Should().Be(HttpStatusCode.Forbidden);
        queryService.Verify(x => x.ExecuteAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        authorizer.VerifyAll();
    }

    [Test]
    public async Task MutationRoute_AuthorizedCaller_PassesAuthenticatedSenderContextAndExecutesMutation()
    {
        var metadata = CreateMetadata();
        var context = new BoltInboundRequestContext(Guid.NewGuid(), BoltCodec.Fnv1aHash("portal"));
        var cancellationToken = new CancellationTokenSource().Token;
        var expectedResponse = new byte[] { 4, 5, 6 };
        var (client, authorizer, queryService, handler) = CreateHandler("__db_changes__");
        await using var disposableClient = client;

        authorizer
            .Setup(x => x.AuthorizeAsync(
                It.Is<InvocationCredentials>(value => HasExpectedCredentials(value)),
                It.Is<RequestMetadata>(value => HasSameMetadata(value, metadata)),
                context,
                It.Is<InvocationAuthorizationPolicy>(policy =>
                    HasPolicy(policy, XFrameworkServiceScopes.DataContextMutate)),
                cancellationToken))
            .ReturnsAsync(Authorized(metadata));
        queryService
            .Setup(x => x.ExecuteChangesAsync(It.IsAny<byte[]>(), cancellationToken))
            .ReturnsAsync(expectedResponse);

        var requestPayload = MemoryPackSerializer.Serialize(new SaveChangesRequest { Metadata = metadata });
        var payload = Envelope(requestPayload);
        var result = await handler(payload, context, cancellationToken);

        result.Item1.Should().Be(HttpStatusCode.OK);
        result.Item2.ToArray().Should().Equal(expectedResponse);
        authorizer.VerifyAll();
        queryService.Verify(x => x.ExecuteChangesAsync(It.Is<byte[]>(bytes => bytes.SequenceEqual(requestPayload)), cancellationToken), Times.Once);
        queryService.Verify(x => x.ExecuteAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task QueryRoute_ActorlessTargeting_UsesScopedServiceTargetPolicy()
    {
        var metadata = CreateMetadata();
        var context = new BoltInboundRequestContext(Guid.NewGuid(), BoltCodec.Fnv1aHash("portal"));
        var (client, authorizer, queryService, handler) = CreateHandler("__db_query__");
        await using var disposableClient = client;

        authorizer
            .Setup(x => x.AuthorizeAsync(
                It.Is<InvocationCredentials>(value => HasExpectedCredentials(value, hasActor: false)),
                It.Is<RequestMetadata>(value => HasSameMetadata(value, metadata)),
                context,
                It.Is<InvocationAuthorizationPolicy>(policy =>
                    HasServiceTargetPolicy(
                        policy,
                        XFrameworkServiceScopes.DataContextQuery,
                        XFrameworkServiceScopes.TenantTarget)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthorizedService(metadata));
        queryService
            .Setup(x => x.ExecuteAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2, 3]);

        var requestPayload = MemoryPackSerializer.Serialize(new QueryDescriptor
        {
            EntityTypeName = "Tenant",
            Metadata = metadata
        });
        var result = await handler(Envelope(requestPayload, actorAccessToken: null), context, CancellationToken.None);

        result.Item1.Should().Be(HttpStatusCode.OK);
        queryService.Verify(x => x.ExecuteAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        authorizer.VerifyAll();
    }

    [Test]
    public async Task MutationRoute_ActorlessTargeting_UsesScopedServiceTargetPolicy()
    {
        var metadata = CreateMetadata();
        var context = new BoltInboundRequestContext(Guid.NewGuid(), BoltCodec.Fnv1aHash("portal"));
        var (client, authorizer, queryService, handler) = CreateHandler("__db_changes__");
        await using var disposableClient = client;

        authorizer
            .Setup(x => x.AuthorizeAsync(
                It.Is<InvocationCredentials>(value => HasExpectedCredentials(value, hasActor: false)),
                It.Is<RequestMetadata>(value => HasSameMetadata(value, metadata)),
                context,
                It.Is<InvocationAuthorizationPolicy>(policy =>
                    HasServiceTargetPolicy(
                        policy,
                        XFrameworkServiceScopes.DataContextMutate,
                        XFrameworkServiceScopes.TenantTarget)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthorizedService(metadata));
        queryService
            .Setup(x => x.ExecuteChangesAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([4, 5, 6]);

        var requestPayload = MemoryPackSerializer.Serialize(new SaveChangesRequest { Metadata = metadata });
        var result = await handler(Envelope(requestPayload, actorAccessToken: null), context, CancellationToken.None);

        result.Item1.Should().Be(HttpStatusCode.OK);
        queryService.Verify(x => x.ExecuteChangesAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        authorizer.VerifyAll();
    }

    [Test]
    public async Task Register_DoesNotExposeUnauthenticatedRemoteQueryStream()
    {
        var authorizer = new Mock<IBoltServiceInvocationAuthorizer>(MockBehavior.Strict);
        var queryService = new Mock<IQueryExecutionService>(MockBehavior.Strict);
        var services = new ServiceCollection()
            .AddSingleton(authorizer.Object)
            .AddSingleton(queryService.Object)
            .BuildServiceProvider();
        await using var client = new BoltClient(
            new Uri("ws://localhost/bolt"),
            "identityserver",
            "IdentityServer",
            new BoltClientOptions(),
            NullLogger<BoltClient>.Instance);

        new DataContextBoltHandler().Register(
            client,
            NullLogger<DataContextBoltHandler>.Instance,
            services.GetRequiredService<IServiceScopeFactory>());

        var field = typeof(BoltClient).GetField("_streamHandlers", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        var handlers = field!.GetValue(client).Should()
            .BeOfType<ConcurrentDictionary<int, Func<BoltStream, Task>>>()
            .Subject;
        handlers.Should().NotContainKey(BoltCodec.Fnv1aHash("__db_query_stream__"),
            "Bolt rejects unregistered stream commands with 501 Not Implemented");
    }

    [Test]
    public async Task QueryRoute_CallerMismatch_ReturnsForbiddenWithoutExecutingQuery()
    {
        var metadata = CreateMetadata();
        var context = new BoltInboundRequestContext(Guid.NewGuid(), BoltCodec.Fnv1aHash("unexpected-caller"));
        var (client, authorizer, queryService, handler) = CreateHandler("__db_query__");
        await using var disposableClient = client;

        authorizer
            .Setup(x => x.AuthorizeAsync(
                It.Is<InvocationCredentials>(value => HasExpectedCredentials(value)),
                It.Is<RequestMetadata>(value => HasSameMetadata(value, metadata)),
                context,
                It.Is<InvocationAuthorizationPolicy>(policy =>
                    HasPolicy(policy, XFrameworkServiceScopes.DataContextQuery)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TrustedInvocationResult.Failure("Service token caller does not match the authenticated Bolt sender.", 403));

        var requestPayload = MemoryPackSerializer.Serialize(new QueryDescriptor
        {
            EntityTypeName = "Tenant",
            Metadata = metadata
        });
        var payload = Envelope(requestPayload);
        var result = await handler(payload, context, CancellationToken.None);

        result.Item1.Should().Be(HttpStatusCode.Forbidden);
        MemoryPackSerializer.Deserialize<DataContextResult>(result.Item2.Span)!.Message
            .Should().Contain("does not match");
        authorizer.VerifyAll();
        queryService.Verify(x => x.ExecuteAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        queryService.Verify(x => x.ExecuteChangesAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task MutationRoute_AuthorizationFailure_ReturnsForbiddenWithoutExecutingMutation()
    {
        var metadata = CreateMetadata();
        var context = new BoltInboundRequestContext(Guid.NewGuid(), BoltCodec.Fnv1aHash("unauthorized-caller"));
        var (client, authorizer, queryService, handler) = CreateHandler("__db_changes__");
        await using var disposableClient = client;

        authorizer
            .Setup(x => x.AuthorizeAsync(
                It.Is<InvocationCredentials>(value => HasExpectedCredentials(value)),
                It.Is<RequestMetadata>(value => HasSameMetadata(value, metadata)),
                context,
                It.Is<InvocationAuthorizationPolicy>(policy =>
                    HasPolicy(policy, XFrameworkServiceScopes.DataContextMutate)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TrustedInvocationResult.Failure("Service caller is not allowed.", 403));

        var requestPayload = MemoryPackSerializer.Serialize(new SaveChangesRequest { Metadata = metadata });
        var payload = Envelope(requestPayload);
        var result = await handler(payload, context, CancellationToken.None);

        result.Item1.Should().Be(HttpStatusCode.Forbidden);
        MemoryPackSerializer.Deserialize<DataContextResult>(result.Item2.Span)!.Message
            .Should().Contain("not allowed");
        authorizer.VerifyAll();
        queryService.Verify(x => x.ExecuteChangesAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        queryService.Verify(x => x.ExecuteAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (
        BoltClient Client,
        Mock<IBoltServiceInvocationAuthorizer> Authorizer,
        Mock<IQueryExecutionService> QueryService,
        Func<ReadOnlyMemory<byte>, BoltInboundRequestContext, CancellationToken, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>> Handler)
        CreateHandler(string commandName)
    {
        var authorizer = new Mock<IBoltServiceInvocationAuthorizer>(MockBehavior.Strict);
        var queryService = new Mock<IQueryExecutionService>(MockBehavior.Strict);
        var services = new ServiceCollection()
            .AddSingleton(authorizer.Object)
            .AddSingleton(queryService.Object)
            .BuildServiceProvider();
        var client = new BoltClient(
            new Uri("ws://localhost/bolt"),
            "identityserver",
            "IdentityServer",
            new BoltClientOptions(),
            NullLogger<BoltClient>.Instance);

        new DataContextBoltHandler().Register(
            client,
            NullLogger<DataContextBoltHandler>.Instance,
            services.GetRequiredService<IServiceScopeFactory>());

        var field = typeof(BoltClient).GetField("_handlers", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        var handlers = field!.GetValue(client).Should()
            .BeOfType<ConcurrentDictionary<int, Func<ReadOnlyMemory<byte>, BoltInboundRequestContext, CancellationToken, Task<(HttpStatusCode, ReadOnlyMemory<byte>)>>>>()
            .Subject;
        handlers.TryGetValue(BoltCodec.Fnv1aHash(commandName), out var handler).Should().BeTrue();

        return (client, authorizer, queryService, handler!);
    }

    private static RequestMetadata CreateMetadata() => new()
    {
        RequestedTenantId = Guid.NewGuid(),
        RequestId = Guid.NewGuid(),
        OperationName = "DataContextBoltHandlerTests"
    };

    private static bool HasSameMetadata(RequestMetadata actual, RequestMetadata expected) =>
        actual.RequestedTenantId == expected.RequestedTenantId &&
        actual.RequestId == expected.RequestId &&
        actual.OperationName == expected.OperationName;

    private static bool HasExpectedCredentials(InvocationCredentials credentials, bool hasActor = true) =>
        credentials.ActorAccessToken == (hasActor ? "actor-token" : null) &&
        credentials.ServiceAccessToken == "service-token";

    private static bool HasPolicy(InvocationAuthorizationPolicy policy, params string[] scopes) =>
        policy.ActorRequirement == ActorRequirement.Required &&
        policy.TenantAccessMode == TenantAccessMode.DelegatedTenant &&
        policy.RequiredActorCapabilities.SequenceEqual(
            scopes.Contains(XFrameworkServiceScopes.DataContextQueryAllTenants)
                ? ["identity.tenants:manage"]
                : []) &&
        policy.RequiredCrossTenantActorCapabilities.SequenceEqual(["identity.tenants:manage"]) &&
        policy.RequiredServiceScopes.Count == scopes.Length &&
        scopes.All(policy.RequiredServiceScopes.Contains);

    private static bool HasServiceTargetPolicy(
        InvocationAuthorizationPolicy policy,
        params string[] scopes) =>
        policy.ActorRequirement == ActorRequirement.None &&
        policy.TenantAccessMode == TenantAccessMode.ServiceTargetTenant &&
        policy.AllowedServiceCallers.SequenceEqual(XFrameworkServiceNames.All) &&
        policy.RequiredServiceScopes.Count == scopes.Length &&
        scopes.All(policy.RequiredServiceScopes.Contains);

    private static byte[] Envelope(byte[] payload, string? actorAccessToken = "actor-token") =>
        MemoryPackSerializer.Serialize(new BoltInvocationEnvelope
        {
            Payload = payload,
            ActorAccessToken = actorAccessToken,
            ServiceAccessToken = "service-token"
        });

    private static TrustedInvocationResult Authorized(RequestMetadata metadata)
    {
        var tenantId = metadata.RequestedTenantId!.Value;
        return TrustedInvocationResult.Success(new TrustedInvocationContext(
            new TrustedActorIdentity(
                Guid.NewGuid(),
                Guid.NewGuid(),
                tenantId,
                Guid.NewGuid(),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                "actor-generation",
                DateTimeOffset.UtcNow.AddMinutes(5)),
            new TrustedServiceIdentity(
                "portal",
                "identityserver",
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    XFrameworkServiceScopes.DataContextQuery,
                    XFrameworkServiceScopes.DataContextMutate
                },
                "service-generation"),
            tenantId,
            tenantId,
            metadata.RequestId!.Value));
    }

    private static TrustedInvocationResult AuthorizedService(RequestMetadata metadata)
    {
        var tenantId = metadata.RequestedTenantId!.Value;
        return TrustedInvocationResult.Success(new TrustedInvocationContext(
            null,
            new TrustedServiceIdentity(
                XFrameworkServiceNames.Portal,
                XFrameworkServiceNames.IdentityServer,
                new HashSet<string>(XFrameworkServiceScopes.AdminDefaults, StringComparer.OrdinalIgnoreCase),
                "service-generation"),
            tenantId,
            tenantId,
            metadata.RequestId!.Value));
    }
}
