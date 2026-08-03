using FluentAssertions;
using System.Security.Claims;
using XFramework.Portal.Services;

namespace Portal.E2ETests;

[TestFixture]
[Category("Kind:Integration")]
[Category("Area:PortalContract")]
public sealed class PortalSessionRevocationContractTests
{
    [Test]
    public void SessionClaims_RequireExactAuthenticatedPortalBindings()
    {
        var tenantId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var roleTypeId = Guid.NewGuid();
        var principal = CreatePrincipal(
            tenantId.ToString(),
            credentialId.ToString(),
            sessionId.ToString(),
            roleTypeId.ToString());

        var isValid = PortalIdentitySessionValidator.TryReadSessionClaims(
            principal,
            out var parsedTenantId,
            out var parsedCredentialId,
            out var parsedSessionId,
            out var parsedRoleTypeId);

        isValid.Should().BeTrue();
        parsedTenantId.Should().Be(tenantId);
        parsedCredentialId.Should().Be(credentialId);
        parsedSessionId.Should().Be(sessionId);
        parsedRoleTypeId.Should().Be(roleTypeId);
    }

    [TestCase(null, "credential", "session", "role")]
    [TestCase("tenant", null, "session", "role")]
    [TestCase("tenant", "credential", null, "role")]
    [TestCase("tenant", "credential", "session", null)]
    [TestCase("tenant", "credential", "session", "not-a-guid")]
    public void SessionClaims_FailClosedWhenAnyRequiredBindingIsMissingOrInvalid(
        string? tenant,
        string? credential,
        string? session,
        string? role)
    {
        var principal = CreatePrincipal(
            ResolveClaimValue(tenant),
            ResolveClaimValue(credential),
            ResolveClaimValue(session),
            ResolveClaimValue(role));

        PortalIdentitySessionValidator.TryReadSessionClaims(
                principal,
                out _,
                out _,
                out _,
                out _)
            .Should().BeFalse();
    }

    [Test]
    public void CookieAuthentication_FailsClosedThroughIdentityServerSessionValidation()
    {
        var portalRoot = GetPortalRoot();
        var program = File.ReadAllText(Path.Combine(portalRoot, "Program.cs"));
        var validator = File.ReadAllText(Path.Combine(portalRoot, "Services", "PortalIdentitySessionValidator.cs"));
        var events = File.ReadAllText(Path.Combine(portalRoot, "Services", "PortalCookieAuthenticationEvents.cs"));

        program.Should().Contain("options.EventsType = typeof(PortalCookieAuthenticationEvents);");
        program.Should().Contain("builder.Services.AddScoped<PortalIdentitySessionValidator>();");
        program.Should().Contain("builder.Services.AddScoped<PortalCookieAuthenticationEvents>();");

        validator.Should().Contain("PortalAuthClaims.TenantId");
        validator.Should().Contain("PortalAuthClaims.CredentialId");
        validator.Should().Contain("PortalAuthClaims.SessionId");
        validator.Should().Contain("PortalAuthClaims.RoleTypeId");
        validator.Should().Contain("RoleTypeIds = [roleTypeId]");
        validator.Should().Contain("identityServer.ValidateIdentitySession(request)");
        validator.Should().Contain(".WaitAsync(ValidationTimeout, ct)");
        validator.Should().Contain("response.TenantId == tenantId");
        validator.Should().Contain("response.CredentialId == credentialId");
        validator.Should().Contain("response.SessionId == sessionId");
        validator.Should().Contain("catch (TimeoutException ex)");
        validator.Should().Contain("catch (Exception ex)");
        validator.Should().NotContain("return true;", "validation must not have an availability fallback");

        events.Should().Contain("context.RejectPrincipal();");
        events.Should().Contain("context.HttpContext.SignOutAsync(PortalAuthDefaults.AuthenticationScheme)");
    }

    [Test]
    public void Logout_RevokesTheServerSessionAndAlwaysClearsTheCookie()
    {
        var endpoint = File.ReadAllText(Path.Combine(
            GetPortalRoot(),
            "Extensions",
            "PortalAuthEndpointExtensions.cs"));

        endpoint.Should().Contain("IIdentityServerServiceWrapper identityServer");
        endpoint.Should().Contain("PortalIdentitySessionValidator.TryReadSessionClaims(");
        endpoint.Should().Contain("SessionId = sessionId");
        endpoint.Should().Contain("CredentialId = credentialId");
        endpoint.Should().Contain("TenantId = tenantId");
        endpoint.Should().Contain("identityServer.Logout(request)");
        endpoint.Should().Contain(".WaitAsync(PortalIdentitySessionValidator.ValidationTimeout)");
        endpoint.Should().Contain("finally");
        endpoint.Should().Contain("context.SignOutAsync(PortalAuthDefaults.AuthenticationScheme)");

        endpoint.IndexOf("identityServer.Logout(request)", StringComparison.Ordinal)
            .Should().BeLessThan(
                endpoint.IndexOf("context.SignOutAsync(PortalAuthDefaults.AuthenticationScheme)", StringComparison.Ordinal),
                "remote revocation should be attempted before the local cookie is cleared");
    }

    [Test]
    public void BlazorCircuits_PeriodicallyReuseTheFailClosedSessionValidator()
    {
        var portalRoot = GetPortalRoot();
        var program = File.ReadAllText(Path.Combine(portalRoot, "Program.cs"));
        var provider = File.ReadAllText(Path.Combine(
            portalRoot,
            "Services",
            "PortalRevalidatingAuthenticationStateProvider.cs"));

        program.Should().Contain(
            "builder.Services.AddScoped<AuthenticationStateProvider, PortalRevalidatingAuthenticationStateProvider>();");
        provider.Should().Contain(": RevalidatingServerAuthenticationStateProvider(loggerFactory)");
        provider.Should().Contain("TimeSpan.FromMinutes(1)");
        provider.Should().Contain("scopeFactory.CreateAsyncScope()");
        provider.Should().Contain("GetRequiredService<PortalIdentitySessionValidator>()");
        provider.Should().Contain("validator.ValidateAsync(authenticationState.User, cancellationToken)");
    }

    private static string GetPortalRoot()
    {
        var repositoryRoot = FindRepositoryRoot();
        return Path.Combine(repositoryRoot.FullName, "src", "Presentation", "XFramework.Portal");
    }

    private static ClaimsPrincipal CreatePrincipal(
        string? tenantId,
        string? credentialId,
        string? sessionId,
        string? roleTypeId)
    {
        var claims = new List<Claim>();
        AddClaim(claims, PortalAuthClaims.TenantId, tenantId);
        AddClaim(claims, PortalAuthClaims.CredentialId, credentialId);
        AddClaim(claims, PortalAuthClaims.SessionId, sessionId);
        AddClaim(claims, PortalAuthClaims.RoleTypeId, roleTypeId);
        return new ClaimsPrincipal(new ClaimsIdentity(claims, PortalAuthDefaults.AuthenticationScheme));
    }

    private static void AddClaim(ICollection<Claim> claims, string type, string? value)
    {
        if (value is not null)
        {
            claims.Add(new Claim(type, value));
        }
    }

    private static string? ResolveClaimValue(string? value) => value switch
    {
        null => null,
        "not-a-guid" => value,
        _ => Guid.NewGuid().ToString()
    };

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "XFramework.slnx")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate XFramework repository root.");
    }
}
