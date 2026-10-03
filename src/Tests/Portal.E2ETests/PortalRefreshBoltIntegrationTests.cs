using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Bolt.Client;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using IdentityServer.Integration.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Extensions;
using XFramework.Integration.Security;
using XFramework.Portal.Services;
using XFramework.Portal.Shared;

namespace Portal.E2ETests;

[TestFixture]
[NonParallelizable]
[Category("Kind:ExtendedIntegration")]
[Category("Module:Portal")]
[Category("Area:PortalLive")]
public sealed class PortalRefreshBoltIntegrationTests
{
    [Test]
    public async Task ValidateAndRefreshAsync_RealBoltRotation_UpdatesTicketAndRecoversStaleTab()
    {
        if (Environment.GetEnvironmentVariable("PORTAL_REFRESH_E2E_ENABLED") != "1")
            Assert.Ignore("Set PORTAL_REFRESH_E2E_ENABLED=1 to enable the live Bolt refresh test.");

        var username = RequiredEnvironment("PORTAL_E2E_USERNAME");
        var password = RequiredEnvironment("PORTAL_E2E_PASSWORD");
        await using var services = BuildServices();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await services.GetRequiredService<BoltClient>().ConnectAsync(timeout.Token);
        await using var circuit = services.CreateAsyncScope();
        var scoped = circuit.ServiceProvider;
        var wrapper = scoped.GetRequiredService<IIdentityServerServiceWrapper>();
        var store = services.GetRequiredService<PortalAuthenticationTicketStore>();
        ClaimsPrincipal? principal = null;
        string? ticketKey = null;

        try
        {
            var context = new DefaultHttpContext();
            context.Request.Headers.UserAgent = "Portal live refresh regression";
            var login = await scoped.GetRequiredService<PortalAuthService>()
                .AuthenticateAsync(username, password, false, context, timeout.Token);
            principal = login.Principal;
            login.IsSuccess.Should().BeTrue("the isolated session must be created through PortalAuthService and Bolt");
            principal.Should().NotBeNull();
            PortalIdentitySessionValidator.TryReadSessionClaims(
                principal, out var tenantId, out _, out var sessionId, out var roleId).Should().BeTrue();
            tenantId.Should().Be(PortalBootstrapConstants.AdminTenantId);
            roleId.Should().Be(PortalBootstrapConstants.AdminRoleTypeId);
            var incidentalTenant = scoped.GetRequiredService<RequestMetadata>().RequestedTenantId;
            incidentalTenant.Should().NotBe(tenantId, "ambient query metadata must differ from the authenticated session tenant");
            scoped.GetRequiredService<TestAuthenticationStateProvider>().Principal = principal!;

            ticketKey = await store.StoreAsync(new AuthenticationTicket(
                ClonePrincipal(principal!),
                new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12) },
                PortalAuthDefaults.AuthenticationScheme));
            var staleTab = ClonePrincipal(principal!);
            var initialAccess = Token(principal!, PortalAuthClaims.ActorAccessToken);
            var initialRefresh = Token(principal!, PortalAuthClaims.RefreshToken);
            var healthy = await scoped.GetRequiredService<PortalIdentitySessionValidator>()
                .ValidateAndRefreshAsync(principal, timeout.Token);
            healthy.IsValid.Should().BeTrue("session validation must reach the deployed IdentityServer wrapper");
            healthy.WasRefreshed.Should().BeFalse();

            // Only the validator sees idle time; transport, service tokens, tickets and IdentityServer use real time.
            var idleClock = new IdleValidatorClock();
            new JwtSecurityTokenHandler().ReadJwtToken(initialAccess).ValidTo
                .Should().BeBefore(idleClock.GetUtcNow().UtcDateTime, "the 31-minute clock must force a refresh");
            var rotated = await CreateValidator(scoped, store, idleClock)
                .ValidateAndRefreshAsync(principal, timeout.Token);
            rotated.IsValid.Should().BeTrue("refresh must pass deployed Bolt tenant authorization");
            rotated.WasRefreshed.Should().BeTrue();
            idleClock.Reset();
            scoped.GetRequiredService<RequestMetadata>().RequestedTenantId.Should().Be(incidentalTenant);
            (Token(principal!, PortalAuthClaims.ActorAccessToken) != initialAccess).Should().BeTrue();
            (Token(principal!, PortalAuthClaims.RefreshToken) != initialRefresh).Should().BeTrue();
            var canonical = await store.RetrieveAsync(ticketKey);
            canonical.Should().NotBeNull();
            TokensMatch(canonical!.Principal, principal!).Should().BeTrue("both rotated tokens must reach the canonical ticket");
            canonical.Principal.FindFirst(PortalAuthClaims.SessionId)!.Value.Should().Be(sessionId.ToString());

            // A new scope/store/coordinator cannot recover by reusing the initiating circuit's refresh cache.
            await using var newTab = services.CreateAsyncScope();
            newTab.ServiceProvider.GetRequiredService<TestAuthenticationStateProvider>().Principal = staleTab;
            var reopenedStore = new PortalAuthenticationTicketStore(
                services.GetRequiredService<IDistributedCache>(),
                services.GetRequiredService<IDataProtectionProvider>(), TimeProvider.System);
            var reopenedValidator = CreateValidator(
                newTab.ServiceProvider, reopenedStore, idleClock, new PortalActorTokenRefreshCoordinator());
            var recovered = await reopenedValidator.ValidateAndRefreshAsync(staleTab, timeout.Token);
            recovered.IsValid.Should().BeTrue("a stale new-tab principal must use the stored replacement credentials");
            recovered.WasRefreshed.Should().BeTrue();
            TokensMatch(staleTab, principal!).Should().BeTrue();
            var unchanged = await reopenedStore.RetrieveAsync(ticketKey);
            unchanged.Should().NotBeNull();
            TokensMatch(unchanged!.Principal, principal!).Should().BeTrue("opening a new tab must not rotate again");
            var actor = await newTab.ServiceProvider.GetRequiredService<IActorIdentityProvider>()
                .ValidateAsync(Token(staleTab, PortalAuthClaims.ActorAccessToken), timeout.Token);
            actor.IsValid.Should().BeTrue();
            actor.Identity!.SessionId.Should().Be(sessionId);
            actor.Identity.TenantId.Should().Be(tenantId);
        }
        finally
        {
            if (PortalIdentitySessionValidator.TryReadSessionClaims(
                    principal, out var tenantId, out var credentialId, out var sessionId, out _))
            {
                try
                {
                    // Use the newest stored actor token even if an assertion interrupted circuit synchronization.
                    var current = await store.ReadPrincipalAsync(principal!) ?? principal!;
                    using var actor = scoped.GetRequiredService<PortalActorAccessTokenScope>()
                        .Push(Token(current, PortalAuthClaims.ActorAccessToken));
                    using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    var logout = await wrapper.Logout(new LogoutRequest
                    {
                        SessionId = sessionId,
                        CredentialId = credentialId,
                        Metadata = new RequestMetadata
                        {
                            RequestedTenantId = tenantId,
                            RequestId = Guid.NewGuid(),
                            OperationName = "Clean up isolated Portal refresh test session",
                            DeviceName = Environment.MachineName
                        }
                    }, cleanupTimeout.Token);
                    logout.IsSuccess.Should().BeTrue($"own-session logout returned HTTP {(int)logout.HttpStatusCode}");
                }
                finally
                {
                    services.GetRequiredService<PortalActorTokenRefreshCoordinator>().Remove(sessionId);
                    if (ticketKey is not null) await store.RemoveAsync(ticketKey);
                }
            }
        }
    }

    private static ServiceProvider BuildServices()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BoltConfiguration:ServerUrls:0"] = RequiredEnvironment("PORTAL_REFRESH_E2E_BOLT_URL"),
            ["BoltConfiguration:ClientName"] = XFrameworkServiceNames.Portal,
            ["BoltConfiguration:RequireSecureTransport"] = "true",
            ["BoltConfiguration:MinConnections"] = "1",
            ["BoltConfiguration:MaxConnections"] = "1",
            ["ServiceIdentity:Authority"] = RequiredEnvironment("PORTAL_REFRESH_E2E_IDENTITY_URL"),
            ["ServiceIdentity:ClientId"] = XFrameworkServiceNames.Portal,
            ["ServiceIdentity:GenerationId"] = RequiredEnvironment("PORTAL_REFRESH_E2E_SERVICE_GENERATION"),
            ["ServiceIdentity:ClientSecret"] = RequiredEnvironment("PORTAL_REFRESH_E2E_SERVICE_SECRET"),
            ["ServiceIdentity:DefaultScopes:0"] = XFrameworkServiceScopes.IdentitySessionValidate
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.Replace(ServiceDescriptor.Singleton<ILoggerFactory>(NullLoggerFactory.Instance));
        services.AddXFrameworkBoltClient(configuration, autoConnect: false);
        services.AddIdentityServerWrapperServices();
        services.AddScoped(_ => new RequestMetadata { RequestedTenantId = Guid.NewGuid() });
        services.AddHttpContextAccessor();
        services.AddDistributedMemoryCache();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddSingleton<PortalAuthenticationTicketStore>();
        services.AddSingleton<PortalActorTokenRefreshCoordinator>();
        services.AddScoped(_ => new TestAuthenticationStateProvider());
        services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<TestAuthenticationStateProvider>());
        services.AddScoped<PortalActorContext>();
        services.AddScoped<PortalActorAccessTokenScope>();
        services.AddScoped<Func<PortalIdentitySessionValidator>>(sp => sp.GetRequiredService<PortalIdentitySessionValidator>);
        services.AddScoped<PortalActorAccessTokenProvider>();
        services.Replace(ServiceDescriptor.Scoped<IActorAccessTokenProvider>(sp => sp.GetRequiredService<PortalActorAccessTokenProvider>()));
        services.Replace(ServiceDescriptor.Scoped<IActorAccessTokenScope>(sp => sp.GetRequiredService<PortalActorAccessTokenScope>()));
        services.Replace(ServiceDescriptor.Scoped<IActorIdentityProvider, IdentityServerActorIdentityProvider>());
        services.AddScoped<PortalAuthService>();
        services.AddScoped(sp => CreateValidator(
            sp, sp.GetRequiredService<PortalAuthenticationTicketStore>(), TimeProvider.System));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static PortalIdentitySessionValidator CreateValidator(
        IServiceProvider services, PortalAuthenticationTicketStore store, TimeProvider clock,
        PortalActorTokenRefreshCoordinator? coordinator = null) => new(
        services.GetRequiredService<IActorIdentityProvider>(),
        services.GetRequiredService<IIdentityServerServiceWrapper>(),
        services.GetRequiredService<PortalActorAccessTokenScope>(),
        coordinator ?? services.GetRequiredService<PortalActorTokenRefreshCoordinator>(),
        store, clock, NullLogger<PortalIdentitySessionValidator>.Instance);

    private static string RequiredEnvironment(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value)) Assert.Ignore($"Set {name} to run the live Bolt refresh test.");
        return value!;
    }

    private static ClaimsPrincipal ClonePrincipal(ClaimsPrincipal principal) =>
        new(principal.Identities.Select(identity => identity.Clone()));

    private static string Token(ClaimsPrincipal principal, string claimType) => principal.FindFirst(claimType)!.Value;

    // Boolean assertions deliberately avoid including bearer credentials in NUnit failure output.
    private static bool TokensMatch(ClaimsPrincipal left, ClaimsPrincipal right) =>
        Token(left, PortalAuthClaims.ActorAccessToken) == Token(right, PortalAuthClaims.ActorAccessToken) &&
        Token(left, PortalAuthClaims.RefreshToken) == Token(right, PortalAuthClaims.RefreshToken);

    private sealed class IdleValidatorClock : TimeProvider
    {
        private TimeSpan _offset = TimeSpan.FromMinutes(31);
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.Add(_offset);
        public void Reset() => _offset = TimeSpan.Zero;
    }

    private sealed class TestAuthenticationStateProvider : AuthenticationStateProvider
    {
        public ClaimsPrincipal Principal { get; set; } = new(new ClaimsIdentity());
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(Principal));
    }
}
