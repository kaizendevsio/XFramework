using System.Reflection;
using FluentAssertions;
using IdentityServer.Api.Features.Auth.Logout;
using NUnit.Framework;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace IdentityServer.UnitTests;

public sealed class LogoutAuthorizationContractTests
{
    [Test]
    public void Logout_RequiresAuthenticatedActorWithoutAdministrativeServiceScope()
    {
        var policy = typeof(LogoutEndpoint).GetMethod(nameof(LogoutEndpoint.Handle))!
            .GetCustomAttribute<BoltHandlerAttribute>()!;
        policy.ActorRequirement.Should().Be(ActorRequirement.Required);
        policy.TenantAccessMode.Should().Be(TenantAccessMode.ActorTenant);
        policy.AllowAnonymous.Should().BeFalse();
        policy.RequiredServiceScopes.Should().BeEmpty();
    }
}
