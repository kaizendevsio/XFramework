using FluentAssertions;
using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Contracts.Requests;
using NUnit.Framework;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace IdentityServer.UnitTests;

[TestFixture]
public sealed class SessionLifetimeTests
{
    private static readonly DateTime Now = new(2026, 9, 21, 14, 16, 5, DateTimeKind.Utc);

    [Test]
    public void OpaqueLogin_CarriesThePersistentSessionRequest()
    {
        // The reported "Your session ended. Sign in to sync." Yap signs in through OPAQUE, and the
        // exchange rebuilt the authentication without the caller's session kind, so every OPAQUE
        // sign-in got the 24-hour default cap and the first refresh after it was refused.
        var login = AuthService.OpaqueLoginRequest(new OpaqueAuthRequest
        {
            Stage = "login-finish", UserName = "someone", RoleId = Guid.NewGuid(), PersistentSession = true
        });

        login.PersistentSession.Should().BeTrue();
        AuthService.NewSessionExpiry(login, Service(), Now).Should().BeNull();
    }

    [Test]
    public void PersistentSession_FromATrustedService_HasNoAbsoluteExpiry() =>
        AuthService.NewSessionExpiry(new AuthenticateIdentityRequest { PersistentSession = true }, Service(), Now)
            .Should().BeNull();

    [Test]
    public void PersistentSession_FromAnAnonymousCaller_KeepsTheDefaultCap() =>
        AuthService.NewSessionExpiry(new AuthenticateIdentityRequest { PersistentSession = true }, null, Now)
            .Should().Be(Now.AddHours(24), "only a backend that keeps tokens server-side may hold an uncapped session");

    [Test]
    public void RememberMe_KeepsItsThirtyDayCap() =>
        AuthService.NewSessionExpiry(new AuthenticateIdentityRequest { RememberMe = true }, Service(), Now)
            .Should().Be(Now.AddDays(30));

    [Test]
    public void DefaultSession_KeepsItsTwentyFourHourCap() =>
        AuthService.NewSessionExpiry(new AuthenticateIdentityRequest(), Service(), Now)
            .Should().Be(Now.AddHours(24));

    private static TrustedInvocationContext Service() => new(
        Actor: null,
        Service: new TrustedServiceIdentity("XFramework.Yap", XFrameworkServiceNames.IdentityServer, new HashSet<string>(), GenerationId: null),
        EffectiveTenantId: Guid.NewGuid(),
        RequestedTargetTenantId: null,
        CorrelationId: Guid.NewGuid());
}
