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
                It.Is<RequestMetadata>(value => HasSameIdentity(value, metadata)),
                context,
                It.Is<IReadOnlyCollection<string>>(scopes =>
                    scopes.Count == 1 && scopes.Contains(XFrameworkServiceScopes.DataContextQuery)),
                null,
                cancellationToken))
            .ReturnsAsync(Authorized(metadata));
        queryService
            .Setup(x => x.ExecuteAsync(It.IsAny<byte[]>(), cancellationToken))
            .ReturnsAsync(expectedResponse);

        var payload = MemoryPackSerializer.Serialize(new QueryDescriptor
        {
            EntityTypeName = "Tenant",
            Metadata = metadata
        });
        var result = await handler(payload, context, cancellationToken);

        result.Item1.Should().Be(HttpStatusCode.OK);
        result.Item2.ToArray().Should().Equal(expectedResponse);
        authorizer.VerifyAll();
        queryService.Verify(x => x.ExecuteAsync(It.Is<byte[]>(bytes => bytes.SequenceEqual(payload)), cancellationToken), Times.Once);
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
                It.Is<RequestMetadata>(value => HasSameIdentity(value, metadata)),
                context,
                It.Is<IReadOnlyCollection<string>>(scopes =>
                    scopes.Count == 2 &&
                    scopes.Contains(XFrameworkServiceScopes.DataContextQuery) &&
                    scopes.Contains(XFrameworkServiceScopes.DataContextQueryAllTenants)),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TrustedServiceInvocationResult.Failure("missing cross-tenant scope", 403));

        var payload = MemoryPackSerializer.Serialize(new QueryDescriptor
        {
            EntityTypeName = "Tenant",
            IgnoreQueryFilters = true,
            Metadata = metadata
        });
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
                It.Is<RequestMetadata>(value => HasSameIdentity(value, metadata)),
                context,
                It.Is<IReadOnlyCollection<string>>(scopes =>
                    scopes.Count == 1 && scopes.Contains(XFrameworkServiceScopes.DataContextMutate)),
                null,
                cancellationToken))
            .ReturnsAsync(Authorized(metadata));
        queryService
            .Setup(x => x.ExecuteChangesAsync(It.IsAny<byte[]>(), cancellationToken))
            .ReturnsAsync(expectedResponse);

        var payload = MemoryPackSerializer.Serialize(new SaveChangesRequest { Metadata = metadata });
        var result = await handler(payload, context, cancellationToken);

        result.Item1.Should().Be(HttpStatusCode.OK);
        result.Item2.ToArray().Should().Equal(expectedResponse);
        authorizer.VerifyAll();
        queryService.Verify(x => x.ExecuteChangesAsync(It.Is<byte[]>(bytes => bytes.SequenceEqual(payload)), cancellationToken), Times.Once);
        queryService.Verify(x => x.ExecuteAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
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
                It.Is<RequestMetadata>(value => HasSameIdentity(value, metadata)),
                context,
                It.Is<IReadOnlyCollection<string>>(scopes =>
                    scopes.Count == 1 && scopes.Contains(XFrameworkServiceScopes.DataContextQuery)),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TrustedServiceInvocationResult.Failure("Service token caller does not match the authenticated Bolt sender.", 403));

        var payload = MemoryPackSerializer.Serialize(new QueryDescriptor
        {
            EntityTypeName = "Tenant",
            Metadata = metadata
        });
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
                It.Is<RequestMetadata>(value => HasSameIdentity(value, metadata)),
                context,
                It.Is<IReadOnlyCollection<string>>(scopes =>
                    scopes.Count == 1 && scopes.Contains(XFrameworkServiceScopes.DataContextMutate)),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TrustedServiceInvocationResult.Failure("Service caller is not allowed.", 403));

        var payload = MemoryPackSerializer.Serialize(new SaveChangesRequest { Metadata = metadata });
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
        TenantId = Guid.NewGuid(),
        CredentialId = Guid.NewGuid(),
        ServiceAccessToken = "service-token"
    };

    private static bool HasSameIdentity(RequestMetadata actual, RequestMetadata expected) =>
        actual.TenantId == expected.TenantId &&
        actual.CredentialId == expected.CredentialId &&
        actual.ServiceAccessToken == expected.ServiceAccessToken;

    private static TrustedServiceInvocationResult Authorized(RequestMetadata metadata) =>
        TrustedServiceInvocationResult.Success(new TrustedServiceInvocation(
            "portal",
            "identityserver",
            metadata.TenantId,
            metadata.CredentialId,
            metadata,
            new HashSet<string>(StringComparer.Ordinal) { XFrameworkServiceScopes.DataContextQuery, XFrameworkServiceScopes.DataContextMutate }));
}
