using FluentAssertions;
using IdentityServer.Api.Features.ServiceIdentity.GetSigningKeys;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using XFramework.Domain.Shared.ServiceIdentity;
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
        var endpoint = typeof(GetServiceSigningKeysHttpEndpoint)
            .GetMethod(nameof(GetServiceSigningKeysHttpEndpoint.HandleHttp))!;
        var attribute = endpoint.GetCustomAttributes(typeof(MapPostAttribute), inherit: false)
            .Cast<MapPostAttribute>()
            .Single();

        attribute.AllowAnonymous.Should().BeTrue();
        attribute.ActorRequirement.Should().Be(ActorRequirement.None);
        attribute.TenantAccessMode.Should().Be(TenantAccessMode.Tenantless);
        attribute.RequiredServiceScopes.Should().BeNull();
        attribute.AllowedServiceCallers.Should().BeNull();
    }

    [Test]
    public async Task HttpSigningKeyDiscovery_WithoutAuthorization_ReturnsPublicKeys()
    {
        using var client = new HttpClient { BaseAddress = new Uri(IntegrationTestFixture.IdentityServerUrl) };

        using var response = await client.PostAsJsonAsync(
            "/api/service-identity/signing-keys/query",
            new GetServiceSigningKeysRequest());

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }
}
