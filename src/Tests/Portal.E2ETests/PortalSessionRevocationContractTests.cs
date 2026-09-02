using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Text.Json;
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
        validator.Should().Contain("IActorIdentityProvider actorIdentityProvider");
        validator.Should().Contain("PortalAuthClaims.ActorAccessToken");
        validator.Should().Contain("PortalAuthClaims.RefreshToken");
        validator.Should().Contain("actorIdentityProvider.ValidateAsync(");
        validator.Should().Contain("identityServer.RefreshToken(");
        validator.Should().Contain("actorAccessTokenProvider.Suppress()");
        validator.Should().Contain("refreshCoordinator.RefreshAsync(");
        validator.Should().Contain("timeout.CancelAfter(ValidationTimeout)");
        validator.Should().Contain("actor.TenantId == tenantId");
        validator.Should().Contain("actor.CredentialId == credentialId");
        validator.Should().Contain("actor.SessionId == sessionId");
        validator.Should().Contain("catch (OperationCanceledException ex)");
        validator.Should().Contain("catch (Exception ex)");
        validator.Should().NotContain("return true;", "validation must not have an availability fallback");

        events.Should().Contain("context.RejectPrincipal();");
        events.Should().Contain("context.HttpContext.SignOutAsync(PortalAuthDefaults.AuthenticationScheme)");
        events.Should().Contain("context.ShouldRenew = validation.WasRefreshed;");
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
        provider.Should().Contain("validator.ValidateAndRefreshAsync(");
    }

    [Test]
    public async Task BlazorCircuitActorToken_DoesNotDependOnAnActiveHttpRequest()
    {
        var credentialId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var actorToken = "actor-token";
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(PortalAuthClaims.CredentialId, credentialId.ToString()),
            new Claim(PortalAuthClaims.SessionId, sessionId.ToString()),
            new Claim(PortalAuthClaims.ActorAccessToken, actorToken)
        ], PortalAuthDefaults.AuthenticationScheme));
        var actorContext = new PortalActorContext(
            new HttpContextAccessor(),
            new FixedAuthenticationStateProvider(principal));
        var tokenProvider = new PortalActorAccessTokenProvider(actorContext);

        var token = await tokenProvider.GetTokenAsync();

        token.Should().Be(actorToken);
        actorContext.CredentialId.Should().Be(credentialId);
        actorContext.SessionId.Should().Be(sessionId);

        using (tokenProvider.Push("validation-token"))
        {
            (await tokenProvider.GetTokenAsync()).Should().Be("validation-token");
        }

        (await tokenProvider.GetTokenAsync()).Should().Be(actorToken);

        using (tokenProvider.Suppress())
        {
            (await tokenProvider.GetTokenAsync()).Should().BeNull();
        }

        (await tokenProvider.GetTokenAsync()).Should().Be(actorToken);
    }

    [Test]
    public async Task ConcurrentRefreshes_WithTheSameRotatedToken_CallIdentityServerOnce()
    {
        var coordinator = new PortalActorTokenRefreshCoordinator();
        var sessionId = Guid.NewGuid();
        var refreshCalls = 0;
        var releaseRefresh = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<PortalActorTokenPair?> Refresh(CancellationToken ct)
        {
            Interlocked.Increment(ref refreshCalls);
            await releaseRefresh.Task.WaitAsync(ct);
            return new PortalActorTokenPair("new-access", "new-refresh", sessionId, 1800);
        }

        var first = coordinator.RefreshAsync(sessionId, "old-refresh", Refresh, CancellationToken.None);
        var second = coordinator.RefreshAsync(sessionId, "old-refresh", Refresh, CancellationToken.None);
        releaseRefresh.SetResult();

        var results = await Task.WhenAll(first, second);

        refreshCalls.Should().Be(1);
        results.Should().NotContainNulls();
        results.Select(result => result!.RefreshToken).Should().OnlyContain(token => token == "new-refresh");
    }

    [Test]
    public async Task RefreshCoordinator_DoesNotExposeCachedTokensForAnUnrelatedCredential()
    {
        var coordinator = new PortalActorTokenRefreshCoordinator();
        var sessionId = Guid.NewGuid();
        var expected = new PortalActorTokenPair("new-access", "new-refresh", sessionId, 1800);
        var first = await coordinator.RefreshAsync(
            sessionId,
            "old-refresh",
            _ => Task.FromResult<PortalActorTokenPair?>(expected),
            CancellationToken.None);
        var unexpectedRefreshCalls = 0;

        var unrelated = await coordinator.RefreshAsync(
            sessionId,
            "unrelated-refresh",
            _ =>
            {
                Interlocked.Increment(ref unexpectedRefreshCalls);
                return Task.FromResult<PortalActorTokenPair?>(expected);
            },
            CancellationToken.None);

        first.Should().Be(expected);
        unrelated.Should().BeNull();
        unexpectedRefreshCalls.Should().Be(0);
    }

    [Test]
    public void PortalLogin_PersistsTheRotatingRefreshCredentialInTheProtectedTicket()
    {
        var portalRoot = GetPortalRoot();
        var authService = File.ReadAllText(Path.Combine(portalRoot, "Services", "PortalAuthService.cs"));
        var claims = File.ReadAllText(Path.Combine(portalRoot, "Services", "PortalAuthClaims.cs"));

        claims.Should().Contain("public const string RefreshToken");
        authService.Should().Contain("response.Response.RefreshToken");
        authService.Should().Contain("new(PortalAuthClaims.RefreshToken, response.RefreshToken!)");
    }

    [Test]
    public void IdentityServerRefreshCredential_OutlivesTheShortLivedAccessToken()
    {
        var repositoryRoot = FindRepositoryRoot();
        var identityServerRoot = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Modules",
            "XFramework.IdentityServer",
            "IdentityServer.Api");

        foreach (var fileName in new[]
                 {
                     "appsettings.json",
                     "appsettings.Development.json",
                     "appsettings.Staging.json"
                 })
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(identityServerRoot, fileName)));
            var jwtOptions = document.RootElement.GetProperty("JwtOptions");
            var accessLifetime = TimeSpan.Parse(jwtOptions.GetProperty("AccessTokenLifespan").GetString()!);
            var refreshLifetime = TimeSpan.Parse(jwtOptions.GetProperty("RefreshTokenLifespan").GetString()!);

            refreshLifetime.Should().BeGreaterThan(accessLifetime, fileName);
            refreshLifetime.Should().BeGreaterThanOrEqualTo(TimeSpan.FromDays(14), fileName);
        }
    }

    [Test]
    public async Task BlazorCircuitActorToken_TakesPrecedenceOverRequestTimePrincipal()
    {
        var requestPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(PortalAuthClaims.ActorAccessToken, "request-token")],
            PortalAuthDefaults.AuthenticationScheme));
        var circuitPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(PortalAuthClaims.ActorAccessToken, "circuit-token")],
            PortalAuthDefaults.AuthenticationScheme));
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = requestPrincipal }
        };
        var actorContext = new PortalActorContext(
            httpContextAccessor,
            new FixedAuthenticationStateProvider(circuitPrincipal));

        var token = await new PortalActorAccessTokenProvider(actorContext).GetTokenAsync();

        token.Should().Be("circuit-token");
    }

    [Test]
    public async Task BackgroundScope_WithoutCircuitOrRequest_HasNoActorToken()
    {
        var actorContext = new PortalActorContext(
            new HttpContextAccessor(),
            new UninitializedAuthenticationStateProvider());

        var token = await new PortalActorAccessTokenProvider(actorContext).GetTokenAsync();

        token.Should().BeNull();
        actorContext.CredentialId.Should().BeNull();
        actorContext.SessionId.Should().BeNull();
    }

    [Test]
    public void PortalActorTokenProvider_IsCircuitScopedAndUsesAuthenticationState()
    {
        var portalRoot = GetPortalRoot();
        var program = File.ReadAllText(Path.Combine(portalRoot, "Program.cs"));
        var context = File.ReadAllText(Path.Combine(portalRoot, "Services", "PortalActorContext.cs"));

        program.Should().Contain("builder.Services.AddScoped<PortalActorContext>();");
        program.Should().Contain("builder.Services.AddScoped<PortalActorAccessTokenProvider>();");
        program.Should().Contain("builder.Services.AddSingleton<PortalActorTokenRefreshCoordinator>();");
        program.Should().Contain("ServiceDescriptor.Scoped<IActorAccessTokenProvider>");
        program.Should().Contain("ServiceDescriptor.Scoped<IActorAccessTokenScope>");
        program.Should().NotContain(
            "ServiceDescriptor.Singleton<IActorAccessTokenProvider, PortalActorAccessTokenProvider>()");
        context.Should().Contain("AuthenticationStateProvider authenticationStateProvider");
        context.Should().Contain("authenticationStateProvider.GetAuthenticationStateAsync()");
    }

    private static string GetPortalRoot()
    {
        var repositoryRoot = FindRepositoryRoot();
        return Path.Combine(repositoryRoot.FullName, "src", "Presentation", "XFramework.Portal");
    }

    [Test]
    public void Login_DoesNotQueryRemoteDataContextBeforeActorAuthentication()
    {
        var authService = File.ReadAllText(Path.Combine(
            GetPortalRoot(),
            "Services",
            "PortalAuthService.cs"));

        authService.Should().Contain("PortalBootstrapConstants.AdminTenantId");
        authService.Should().Contain("PortalBootstrapConstants.AdminRoleTypeId");
        authService.Should().NotContain("IDataContext");
        authService.Should().NotContain("IgnoreQueryFilters()");
        authService.Should().NotContain("FindBootstrapTenantAsync");
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

    private sealed class FixedAuthenticationStateProvider(ClaimsPrincipal principal)
        : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(principal));
    }

    private sealed class UninitializedAuthenticationStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            throw new InvalidOperationException("No circuit authentication state is available.");
    }

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
