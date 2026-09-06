using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts;
using NUnit.Framework;
using XFramework.Domain.Shared.Attributes;
using XFramework.Domain.Shared.ServiceIdentity;

namespace IdentityServer.UnitTests;

[TestFixture]
public sealed class ManagedTenantRegistryAuthorizationTests
{
    [TestCase(typeof(RegistryConfiguration))]
    [TestCase(typeof(RegistryConfigurationGroup))]
    public void RegistryEntities_AllowManagedTenantAccess(Type entityType)
    {
        var policy = entityType
            .GetCustomAttributes(typeof(GenerateEndpointsAttribute), false)
            .Cast<GenerateEndpointsAttribute>()
            .Single();

        policy.TenantAccessMode.Should().Be(GeneratedTenantAccessMode.DelegatedTenant);
        policy.CrossTenantCapability.Should().Be(XFrameworkActorCapabilities.IdentityTenantsManage);
    }
}
