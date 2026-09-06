using FluentAssertions;
using NUnit.Framework;
using XFramework.Domain.Shared.Attributes;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;
using XFramework.Inventario.Domain.Shared.Contracts;

namespace Inventario.Api.Tests;

[TestFixture]
public sealed class DelegatedTenantAuthorizationTests
{
    [Test]
    public void InventarioBoltHandlers_RequireManagedTenantDelegation()
    {
        var policies = typeof(global::Inventario.Api.Features.Warehouses.Create.CreateWarehouseEndpoint)
            .Assembly
            .GetTypes()
            .Where(type => type.Namespace?.StartsWith("Inventario.Api.Features", StringComparison.Ordinal) == true)
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
    public void InventarioDataContextEntities_RequireManagedTenantDelegation()
    {
        var policies = typeof(Product)
            .Assembly
            .GetTypes()
            .Where(type =>
                type.Namespace == "XFramework.Inventario.Domain.Shared.Contracts" &&
                type.GetCustomAttributes(typeof(GenerateEndpointsAttribute), false).Length > 0)
            .Select(type => type.GetCustomAttributes(typeof(GenerateEndpointsAttribute), false)
                .Cast<GenerateEndpointsAttribute>()
                .Single())
            .ToList();

        policies.Should().NotBeEmpty();
        policies.Should().OnlyContain(attribute =>
            attribute.TenantAccessMode == GeneratedTenantAccessMode.DelegatedTenant &&
            attribute.CrossTenantCapability == XFrameworkActorCapabilities.IdentityTenantsManage);
    }
}
