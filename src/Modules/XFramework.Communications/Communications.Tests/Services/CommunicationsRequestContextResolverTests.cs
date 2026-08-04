using Communications.Api.Services;
using Communications.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.ServiceIdentity;

namespace Communications.Tests.Services;

public sealed class CommunicationsRequestContextResolverTests
{
    [Test]
    public async Task ResolveAsync_ServiceOnlyContext_ReturnsUnauthorized()
    {
        var tenantId = Guid.NewGuid();
        var resolver = Resolver(new FakeTrustedServiceInvocationResolver(tenantId));

        var result = await resolver.ResolveAsync(Metadata(tenantId));

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(401));
    }

    [Test]
    public async Task ResolveTrustedInternalAsync_ServiceContext_ReturnsContext()
    {
        var tenantId = Guid.NewGuid();
        var resolver = Resolver(new FakeTrustedServiceInvocationResolver(tenantId));

        var result = await resolver.ResolveTrustedInternalAsync(Metadata(tenantId));

        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(result.Data!.TenantId, Is.EqualTo(tenantId));
        Assert.That(result.Data.CredentialId, Is.Null);
        Assert.That(result.Data.IsTrustedInternal, Is.True);
    }

    [Test]
    public async Task ResolveAdminAsync_PortalServiceContext_ReturnsAdminContext()
    {
        var tenantId = Guid.NewGuid();
        var resolver = Resolver(new FakeTrustedServiceInvocationResolver(tenantId));

        var result = await resolver.ResolveAdminAsync(Metadata(tenantId));

        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(result.Data!.TrustedServiceName, Is.EqualTo(XFrameworkServiceNames.Portal));
    }

    [Test]
    public async Task ResolveAdminAsync_NonAdminActorPlusPortalService_ReturnsForbidden()
    {
        var tenantId = Guid.NewGuid();
        var resolver = Resolver(new FakeTrustedServiceInvocationResolver(
            tenantId,
            Guid.NewGuid(),
            includeActor: true));

        var result = await resolver.ResolveAdminAsync(Metadata(tenantId));

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task ResolveTrustedInternalAsync_ActorPlusService_ReturnsForbidden()
    {
        var tenantId = Guid.NewGuid();
        var resolver = Resolver(new FakeTrustedServiceInvocationResolver(
            tenantId,
            Guid.NewGuid(),
            includeActor: true));

        var result = await resolver.ResolveTrustedInternalAsync(Metadata(tenantId));

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task ResolveAdminAsync_DisallowedServiceContext_ReturnsForbidden()
    {
        var tenantId = Guid.NewGuid();
        var resolver = Resolver(new FakeTrustedServiceInvocationResolver(
            tenantId,
            serviceName: XFrameworkServiceNames.Wallets));

        var result = await resolver.ResolveAdminAsync(Metadata(tenantId));

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task ResolveAsync_ActorContext_ReturnsActorTenantAndCredential()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var resolver = Resolver(new FakeTrustedServiceInvocationResolver(
            tenantId,
            credentialId,
            includeActor: true));

        var result = await resolver.ResolveAsync(Metadata(tenantId));

        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(result.Data!.TenantId, Is.EqualTo(tenantId));
        Assert.That(result.Data.CredentialId, Is.EqualTo(credentialId));
    }

    [Test]
    public async Task ResolveAsync_MismatchedRequestedTenant_ReturnsForbidden()
    {
        var tenantId = Guid.NewGuid();
        var resolver = Resolver(new FakeTrustedServiceInvocationResolver(
            tenantId,
            Guid.NewGuid(),
            includeActor: true));

        var result = await resolver.ResolveAsync(Metadata(Guid.NewGuid()));

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(403));
    }

    private static CommunicationsRequestContextResolver Resolver(
        FakeTrustedServiceInvocationResolver invocationContext) =>
        new(
            new HttpContextAccessor(),
            new ConfigurationBuilder().Build(),
            serviceInvocationResolver: invocationContext);

    private static RequestMetadata Metadata(Guid tenantId) => new()
    {
        RequestedTenantId = tenantId,
        OperationName = "Communications test"
    };
}
