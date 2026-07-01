using System.Security.Claims;
using Communications.Api.Services;
using Communications.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Security;

namespace Communications.Tests.Services;

public sealed class CommunicationsRequestContextResolverTests
{
    private const string CommunicationsClientName = "XFramework.Communications";

    [Test]
    public void Resolve_ServiceTokenMetadata_ReturnsUnauthorized()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var metadata = Metadata(tenantId, credentialId, token: FakeTrustedServiceInvocationResolver.ValidControlPanelToken);
        var resolver = Resolver();

        var result = resolver.Resolve(metadata);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(401));
    }

    [Test]
    public void ResolveTrustedInternal_ServiceTokenMetadata_ReturnsContext()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var metadata = Metadata(tenantId, credentialId, token: FakeTrustedServiceInvocationResolver.ValidControlPanelToken);
        var resolver = Resolver();

        var result = resolver.ResolveTrustedInternal(metadata);

        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(result.Data!.TenantId, Is.EqualTo(tenantId));
        Assert.That(result.Data.CredentialId, Is.EqualTo(credentialId));
        Assert.That(result.Data.IsTrustedInternal, Is.True);
    }

    [Test]
    public void ResolveAdmin_ControlPanelServiceTokenMetadata_ReturnsAdminContext()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var metadata = Metadata(tenantId, credentialId, token: FakeTrustedServiceInvocationResolver.ValidControlPanelToken);
        var resolver = Resolver();

        var result = resolver.ResolveAdmin(metadata);

        Assert.That(result.IsSuccess, Is.True, result.Message);
        Assert.That(result.Data!.TenantId, Is.EqualTo(tenantId));
        Assert.That(result.Data.CredentialId, Is.EqualTo(credentialId));
        Assert.That(result.Data.IsTrustedInternal, Is.True);
        Assert.That(result.Data.TrustedServiceName, Is.EqualTo("XFramework.ControlPanel"));
    }

    [Test]
    public void ResolveTrustedInternal_ServiceTokenWithWrongAudience_ReturnsUnauthorized()
    {
        var metadata = Metadata(
            Guid.NewGuid(),
            Guid.NewGuid(),
            token: FakeTrustedServiceInvocationResolver.WrongAudienceToken);
        var resolver = Resolver();

        var result = resolver.ResolveTrustedInternal(metadata);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(401));
    }

    [Test]
    public void ResolveAdmin_ServiceTokenWithWrongServiceName_ReturnsForbidden()
    {
        var metadata = Metadata(Guid.NewGuid(), Guid.NewGuid(), token: FakeTrustedServiceInvocationResolver.OtherServiceToken);
        var resolver = Resolver();

        var result = resolver.ResolveAdmin(metadata);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public void Resolve_UnsignedInternalMetadata_ReturnsUnauthorized()
    {
        var metadata = Metadata(Guid.NewGuid(), Guid.NewGuid());
        var resolver = Resolver();

        var result = resolver.Resolve(metadata);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(401));
    }

    [Test]
    public void Resolve_AuthenticatedUserWithMismatchedTenantMetadata_ReturnsForbidden()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = Principal(tenantId, credentialId)
            }
        };
        var metadata = Metadata(Guid.NewGuid(), credentialId);
        var resolver = Resolver(accessor);

        var result = resolver.Resolve(metadata);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(403));
    }

    [Test]
    public void Resolve_AuthenticatedUserWithMismatchedCredentialMetadata_ReturnsForbidden()
    {
        var tenantId = Guid.NewGuid();
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = Principal(tenantId, Guid.NewGuid())
            }
        };
        var metadata = Metadata(tenantId, Guid.NewGuid());
        var resolver = Resolver(accessor);

        var result = resolver.Resolve(metadata);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(403));
    }

    private static CommunicationsRequestContextResolver Resolver(HttpContextAccessor? accessor = null) =>
        new(
            accessor ?? new HttpContextAccessor(),
            Configuration(),
            serviceInvocationResolver: new FakeTrustedServiceInvocationResolver());

    private static RequestMetadata Metadata(Guid tenantId, Guid credentialId, string? token = null)
    {
        return new RequestMetadata
        {
            TenantId = tenantId,
            CredentialId = credentialId,
            Name = "XFramework.ControlPanel",
            ServiceAccessToken = token
        };
    }

    private static ClaimsPrincipal Principal(Guid tenantId, Guid credentialId) =>
        new(new ClaimsIdentity(
            [
                new Claim("tenantId", tenantId.ToString("D")),
                new Claim(ClaimTypes.Name, credentialId.ToString("D"))
            ],
            "TestAuth"));

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BoltConfiguration:ClientName"] = CommunicationsClientName
            })
            .Build();
}
