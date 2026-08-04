using FluentAssertions;
using IdentityServer.Api.Features.ServiceIdentity.GetSigningKeys;
using NUnit.Framework;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace IdentityServer.IntegrationTests;

[TestFixture]
[Category("Kind:Integration")]
[Category("Module:IdentityServer")]
[Category("Area:ServiceIdentity")]
public sealed class ServiceSigningKeyDiscoveryContractTests
{
    [Test]
    public void HttpSigningKeyDiscovery_IsExplicitlyAnonymousAndTenantless()
    {
        var endpoint = typeof(GetServiceSigningKeysEndpoint)
            .GetMethod(nameof(GetServiceSigningKeysEndpoint.HandleHttp))!;
        var attribute = endpoint.GetCustomAttributes(typeof(MapPostAttribute), inherit: false)
            .Cast<MapPostAttribute>()
            .Single();

        attribute.AllowAnonymous.Should().BeTrue();
        attribute.ActorRequirement.Should().Be(ActorRequirement.None);
        attribute.TenantAccessMode.Should().Be(TenantAccessMode.Tenantless);
        attribute.RequiredServiceScopes.Should().BeNull();
        attribute.AllowedServiceCallers.Should().BeNull();
    }
}
