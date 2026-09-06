using FluentAssertions;
using NUnit.Framework;
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
}
