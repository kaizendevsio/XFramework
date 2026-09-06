using FluentAssertions;
using NUnit.Framework;
using XFramework.Domain.Shared.Attributes;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace POS.Api.Tests;

[TestFixture]
public sealed class DelegatedTenantAuthorizationTests
{
    [Test]
    public void PosBoltHandlers_RequireManagedTenantDelegation()
    {
        var policies = typeof(global::POS.Api.Features.SearchPosCatalogEndpoint)
            .Assembly
            .GetTypes()
            .Where(type => type.Namespace?.StartsWith("POS.Api.Features", StringComparison.Ordinal) == true)
            .SelectMany(type => type.GetMethods())
            .Select(method => method.GetCustomAttributes(typeof(BoltHandlerAttribute), false)
                .Cast<BoltHandlerAttribute>()
                .SingleOrDefault())
            .Where(attribute => attribute is not null)
            .Cast<BoltHandlerAttribute>()
            .ToList();

        policies.Should().NotBeEmpty();
        policies.Should().OnlyContain(attribute =>
            attribute.TenantAccessMode == TenantAccessMode.DelegatedTenant &&
            attribute.RequiredCrossTenantActorCapabilities is
                [XFrameworkActorCapabilities.IdentityTenantsManage]);
    }

    [Test]
    public void PosDataContextEntities_RequireManagedTenantDelegation()
    {
        var policies = typeof(global::POS.Domain.Shared.Contracts.PosRegister)
            .Assembly
            .GetTypes()
            .Where(type =>
                type.Namespace == "POS.Domain.Shared.Contracts" &&
                type.GetCustomAttributes(typeof(GenerateEndpointsAttribute), false).Length > 0)
            .Select(type => type.GetCustomAttributes(typeof(GenerateEndpointsAttribute), false)
                .Cast<GenerateEndpointsAttribute>()
                .Single())
            .ToList();

        policies.Should().NotBeEmpty();
        policies.Should().OnlyContain(attribute =>
            attribute.TenantAccessMode == GeneratedTenantAccessMode.DelegatedTenant &&
            !string.IsNullOrWhiteSpace(attribute.AuthorizationFeature) &&
            attribute.AuthorizationFeature.StartsWith("pos", StringComparison.Ordinal) &&
            attribute.CrossTenantCapability == XFrameworkActorCapabilities.IdentityTenantsManage);
    }
}
