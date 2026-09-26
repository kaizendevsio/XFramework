using Communications.Integration.Clients;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Integration.Drivers;
using MemoryPack;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.Enums;
using XFramework.Integration.Security;
using Yap.Services;

namespace Yap.Tests;

[TestFixture]
public sealed class ChatDirectoryTests
{
    [Test]
    public async Task ResolveAsync_UsesSupportedBoundedMembershipQueriesWithTenantMetadata()
    {
        var tenant = Guid.NewGuid();
        var actor = new CommunicationsChatActor(tenant, Guid.NewGuid(), AccessToken: "test-actor-token");
        var actors = new Mock<ICommunicationsChatActorProvider>();
        actors.Setup(a => a.GetCurrentActorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor);
        var tokenScope = new Mock<IActorAccessTokenScope>();
        tokenScope.Setup(s => s.Push(actor.AccessToken!)).Returns(Mock.Of<IDisposable>());
        var queries = new List<QueryDescriptor>();
        var wrapper = new Mock<IIdentityServerServiceWrapper>();
        wrapper.Setup(w => w.ExecuteQueryAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns((byte[] bytes, CancellationToken _) =>
            {
                queries.Add(MemoryPackSerializer.Deserialize<QueryDescriptor>(bytes)!);
                return Task.FromResult(MemoryPackSerializer.Serialize(new List<IdentityCredential>()));
            });
        using var services = new ServiceCollection().AddSingleton(wrapper.Object).BuildServiceProvider();
        var directory = new ChatDirectory(services, actors.Object, tokenScope.Object);
        var ids = Enumerable.Range(0, 125).Select(_ => Guid.NewGuid()).ToArray();

        await directory.ResolveAsync([.. ids, ids[0]], CancellationToken.None);

        Assert.That(queries, Has.Count.EqualTo(3));
        Assert.That(queries.All(q => q.Take <= 50 && q.Metadata?.RequestedTenantId == tenant && !q.IgnoreQueryFilters), Is.True);
        var queriedIds = queries.SelectMany(q => q.Filters)
            .Where(f => f.PropertyName == nameof(IdentityCredential.Id) && f.Operation == QueryFilterOperation.In)
            .Select(f => (Guid)f.Value!).ToArray();
        Assert.That(queriedIds, Is.EquivalentTo(ids));
        tokenScope.Verify(s => s.Push(actor.AccessToken!), Times.Once);
    }

    // The driver's wording for a refused __db_query__ (ServiceWrapperGenerator). A refused
    // sign-in must reach the host as the 401 it is; anything else stays a service failure.
    [TestCase(401, true)]
    [TestCase(503, false)]
    public void ResolveAsync_RefusedActorToken_SurfacesAsUnauthorized(int status, bool refused)
    {
        var actor = new CommunicationsChatActor(Guid.NewGuid(), Guid.NewGuid(), AccessToken: "test-actor-token");
        var actors = new Mock<ICommunicationsChatActorProvider>();
        actors.Setup(a => a.GetCurrentActorAsync(It.IsAny<CancellationToken>())).ReturnsAsync(actor);
        var tokenScope = new Mock<IActorAccessTokenScope>();
        tokenScope.Setup(s => s.Push(actor.AccessToken!)).Returns(Mock.Of<IDisposable>());
        var wrapper = new Mock<IIdentityServerServiceWrapper>();
        wrapper.Setup(w => w.ExecuteQueryAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException($"DataContext query request failed with status {status} ({(System.Net.HttpStatusCode)status})."));
        using var services = new ServiceCollection().AddSingleton(wrapper.Object).BuildServiceProvider();
        var directory = new ChatDirectory(services, actors.Object, tokenScope.Object);

        var error = Assert.CatchAsync(() => directory.ResolveAsync([Guid.NewGuid()], CancellationToken.None));

        if (refused) Assert.That(error, Is.TypeOf<YapApiException>().With.Property(nameof(YapApiException.Status)).EqualTo(401));
        else Assert.That(error, Is.TypeOf<InvalidOperationException>());
    }
}
