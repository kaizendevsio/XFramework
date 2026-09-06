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
        var handlers = typeof(global::Inventario.Api.Features.Warehouses.Create.CreateWarehouseEndpoint)
            .Assembly
            .GetTypes()
            .Where(type => type.Namespace?.StartsWith("Inventario.Api.Features", StringComparison.Ordinal) == true)
            .SelectMany(type => type.GetMethods())
            .Select(method => new
            {
                Method = method,
                Policy = method.GetCustomAttributes(typeof(BoltHandlerAttribute), false)
                    .Cast<BoltHandlerAttribute>()
                    .SingleOrDefault()
            })
            .Where(item => item.Policy is not null)
            .ToList();

        handlers.Should().NotBeEmpty();
        handlers
            .Where(item =>
                item.Policy!.TenantAccessMode != TenantAccessMode.DelegatedTenant ||
                item.Policy.RequiredCrossTenantActorCapabilities is not
                    [XFrameworkActorCapabilities.IdentityTenantsManage])
            .Select(item => $"{item.Method.DeclaringType?.FullName}.{item.Method.Name}")
            .Should().BeEmpty();
    }

    [Test]
    public void InventarioDataContextEntities_ExposeManagedTenantReads()
    {
        var policies = typeof(Product)
            .Assembly
            .GetTypes()
            .Where(type =>
                type.Namespace == "XFramework.Inventario.Domain.Shared.Contracts" &&
                type.GetCustomAttributes(typeof(GenerateEndpointsAttribute), false).Length > 0)
            .Select(type => new
            {
                Policy = type.GetCustomAttributes(typeof(GenerateEndpointsAttribute), false)
                    .Cast<GenerateEndpointsAttribute>()
                    .Single(),
                AllowsRemoteQuery = Attribute.IsDefined(type, typeof(AllowRemoteDataContextQueryAttribute))
            })
            .ToList();

        policies.Should().NotBeEmpty();
        policies.Should().OnlyContain(item =>
            item.AllowsRemoteQuery &&
            item.Policy.TenantAccessMode == GeneratedTenantAccessMode.DelegatedTenant &&
            !string.IsNullOrWhiteSpace(item.Policy.AuthorizationFeature) &&
            item.Policy.AuthorizationFeature.StartsWith("inventario", StringComparison.Ordinal) &&
            item.Policy.CrossTenantCapability == XFrameworkActorCapabilities.IdentityTenantsManage);
    }
}
