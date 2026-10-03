using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using FluentAssertions;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using IdentityServer.Integration.Drivers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Integration.Security;
using XFramework.Portal.Services;
using XFramework.Portal.Shared;

namespace Portal.E2ETests;

[TestFixture]
[Category("Area:PortalContract")]
public sealed class PortalAuthenticationTicketStoreTests
{
    [Test]
    [Category("Kind:Integration")]
    public async Task RedisTicket_ReloadedApplicationAndProtectionProvider_RetainsRotatedCredential()
    {
        var connection = Environment.GetEnvironmentVariable("PORTAL_TEST_REDIS_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
            Assert.Ignore("Set PORTAL_TEST_REDIS_CONNECTION to run the persistent Redis ticket regression.");

        var keyDirectory = Directory.CreateTempSubdirectory("portal-ticket-regression-");
        using var firstServices = new ServiceCollection()
            .AddStackExchangeRedisCache(options => options.Configuration = connection).BuildServiceProvider();
        using var restartedServices = new ServiceCollection()
            .AddStackExchangeRedisCache(options => options.Configuration = connection).BuildServiceProvider();
        var firstProtection = DataProtectionProvider.Create(keyDirectory,
            options => options.SetApplicationName("PortalTicketRegression"));
        var fixture = new SessionFixture(firstServices.GetRequiredService<IDistributedCache>(), firstProtection);
        string? key = null;
        try
        {
            var ticket = fixture.CreateTicket();
            var original = ClonePrincipal(ticket.Principal);
            key = await fixture.Store.StoreAsync(ticket);
            fixture.Clock.Advance(TimeSpan.FromMinutes(30));
            (await fixture.Validator().ValidateAndRefreshAsync(ticket.Principal)).IsValid.Should().BeTrue();
            fixture.Clock.Advance(TimeSpan.FromMinutes(16));

            var restartedProtection = DataProtectionProvider.Create(keyDirectory,
                options => options.SetApplicationName("PortalTicketRegression"));
            var restarted = new PortalAuthenticationTicketStore(
                restartedServices.GetRequiredService<IDistributedCache>(), restartedProtection, fixture.Clock);

            (await fixture.Validator(restarted).ValidateAndRefreshAsync(original)).IsValid.Should().BeTrue();
            original.FindFirstValue(PortalAuthClaims.RefreshToken).Should().Be("refresh-1");
            fixture.RefreshCalls.Should().Be(1);
        }
        finally
        {
            if (key is not null) await fixture.Store.RemoveAsync(key);
            keyDirectory.Delete(recursive: true);
        }
    }

    [Test]
    public async Task ConcurrentCircuitRefreshes_ReadSameTicket_RotateOnce()
    {
        var fixture = new SessionFixture();
        var ticket = fixture.CreateTicket();
        await fixture.Store.StoreAsync(ticket);
        fixture.Clock.Advance(TimeSpan.FromMinutes(30));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.BeforeRefresh = async () => { started.TrySetResult(); await release.Task; };
        var coordinator = new PortalActorTokenRefreshCoordinator();

        var first = fixture.Validator(coordinator: coordinator).ValidateAndRefreshAsync(ClonePrincipal(ticket.Principal));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = fixture.Validator(coordinator: coordinator).ValidateAndRefreshAsync(ClonePrincipal(ticket.Principal));
        release.SetResult();
        var results = await Task.WhenAll(first, second);

        results.Should().OnlyContain(result => result.IsValid);
        fixture.RefreshCalls.Should().Be(1);
    }

    [Test]
    public async Task CircuitRefresh_IdleThenNewTabWithOriginalPrincipal_UsesPersistedTokens()
    {
        var fixture = new SessionFixture();
        var ticket = fixture.CreateTicket();
        var original = ClonePrincipal(ticket.Principal);
        await fixture.Store.StoreAsync(ticket);
        fixture.Clock.Advance(TimeSpan.FromMinutes(30));
        (await fixture.Validator().ValidateAndRefreshAsync(ticket.Principal)).IsValid.Should().BeTrue();
        fixture.Clock.Advance(TimeSpan.FromMinutes(16));

        var restartedStore = fixture.NewStore();
        var result = await fixture.Validator(restartedStore).ValidateAndRefreshAsync(original);

        result.IsValid.Should().BeTrue();
        original.FindFirstValue(PortalAuthClaims.RefreshToken).Should().Be("refresh-1");
        fixture.RefreshCalls.Should().Be(1);
    }

    [Test]
    public async Task RepeatedCircuitRotations_OriginalPrincipal_ResolvesCurrentCredential()
    {
        var fixture = new SessionFixture();
        var ticket = fixture.CreateTicket();
        var original = ClonePrincipal(ticket.Principal);
        await fixture.Store.StoreAsync(ticket);
        fixture.Clock.Advance(TimeSpan.FromMinutes(30));
        (await fixture.Validator().ValidateAndRefreshAsync(ticket.Principal)).IsValid.Should().BeTrue();
        fixture.Clock.Advance(TimeSpan.FromMinutes(30));

        (await fixture.Validator(fixture.NewStore()).ValidateAndRefreshAsync(original)).IsValid.Should().BeTrue();

        original.FindFirstValue(PortalAuthClaims.RefreshToken).Should().Be("refresh-2");
        fixture.RefreshCalls.Should().Be(2);
    }

    [Test]
    public async Task DelayedCookieRenewal_AfterCircuitRefresh_PreservesTokensAndCookieProperties()
    {
        var fixture = new SessionFixture();
        var ticket = fixture.CreateTicket();
        ticket.Properties.IsPersistent = true;
        var key = await fixture.Store.StoreAsync(ticket);
        var renewal = (await fixture.Store.RetrieveAsync(key))!;
        fixture.Clock.Advance(TimeSpan.FromMinutes(30));
        await fixture.Validator().ValidateAndRefreshAsync(ticket.Principal);
        renewal.Properties.ExpiresUtc = fixture.Clock.GetUtcNow().AddDays(14);

        await fixture.Store.RenewAsync(key, renewal);

        var stored = (await fixture.Store.RetrieveAsync(key))!;
        stored.Principal.FindFirstValue(PortalAuthClaims.RefreshToken).Should().Be("refresh-1");
        stored.Properties.ExpiresUtc.Should().Be(renewal.Properties.ExpiresUtc);
        stored.Properties.IsPersistent.Should().BeTrue();
    }

    [Test]
    public async Task TicketExpiry_AfterRefresh_RejectsCircuitsAndLateRenewal()
    {
        var fixture = new SessionFixture();
        var ticket = fixture.CreateTicket();
        ticket.Properties.ExpiresUtc = fixture.Clock.GetUtcNow().AddMinutes(31);
        var key = await fixture.Store.StoreAsync(ticket);
        fixture.Clock.Advance(TimeSpan.FromMinutes(30));
        (await fixture.Validator().ValidateAndRefreshAsync(ticket.Principal)).IsValid.Should().BeTrue();
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));

        (await fixture.Store.RetrieveAsync(key)).Should().BeNull();
        (await fixture.Validator().ValidateAndRefreshAsync(ticket.Principal)).IsValid.Should().BeFalse();
        ticket.Properties.ExpiresUtc = fixture.Clock.GetUtcNow().AddHours(12);
        await fixture.Store.RenewAsync(key, ticket);
        (await fixture.Store.RetrieveAsync(key)).Should().BeNull();
        fixture.RefreshCalls.Should().Be(1);
    }

    [Test]
    public async Task Logout_DuringRotation_DoesNotRestoreTicket()
    {
        var fixture = new SessionFixture();
        var ticket = fixture.CreateTicket();
        var key = await fixture.Store.StoreAsync(ticket);
        fixture.Clock.Advance(TimeSpan.FromMinutes(30));
        fixture.BeforeRefresh = () => fixture.Store.RemoveAsync(key);

        (await fixture.Validator().ValidateAndRefreshAsync(ticket.Principal)).IsValid.Should().BeFalse();
        await fixture.Store.RenewAsync(key, ticket);
        (await fixture.Store.RetrieveAsync(key)).Should().BeNull();
    }

    [Test]
    public async Task BrowserDisconnect_AfterRotationStarts_PersistsReplacementCredential()
    {
        var fixture = new SessionFixture();
        var ticket = fixture.CreateTicket();
        var key = await fixture.Store.StoreAsync(ticket);
        var original = ClonePrincipal(ticket.Principal);
        fixture.Clock.Advance(TimeSpan.FromMinutes(30));
        using var canceled = new CancellationTokenSource();
        fixture.BeforeRefresh = () => { canceled.Cancel(); return Task.CompletedTask; };

        var validation = () => fixture.Validator().ValidateAndRefreshAsync(ticket.Principal, canceled.Token);
        await validation.Should().ThrowAsync<OperationCanceledException>();

        var stored = (await fixture.NewStore().RetrieveAsync(key))!;
        stored.Principal.FindFirstValue(PortalAuthClaims.RefreshToken).Should().Be("refresh-1");
        (await fixture.Validator().ValidateAndRefreshAsync(original)).IsValid.Should().BeTrue();
        fixture.RefreshCalls.Should().Be(1);
    }

    [Test]
    public async Task CookieValidation_BrowserAbortsDuringRotation_DoesNotLogOutOtherTabs()
    {
        var fixture = new SessionFixture();
        var ticket = fixture.CreateTicket();
        var key = await fixture.Store.StoreAsync(ticket);
        fixture.Clock.Advance(TimeSpan.FromMinutes(30));
        using var canceled = new CancellationTokenSource();
        fixture.BeforeRefresh = () => { canceled.Cancel(); return Task.CompletedTask; };
        var http = new DefaultHttpContext { RequestAborted = canceled.Token };
        var authentication = new Mock<IAuthenticationService>(MockBehavior.Strict);
        using var services = new ServiceCollection().AddSingleton(authentication.Object).BuildServiceProvider();
        http.RequestServices = services;
        var context = new CookieValidatePrincipalContext(http,
            new AuthenticationScheme(PortalAuthDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler)),
            new CookieAuthenticationOptions { SessionStore = fixture.Store }, ticket);
        var events = new PortalCookieAuthenticationEvents(
            fixture.Validator(), NullLogger<PortalCookieAuthenticationEvents>.Instance);

        var validation = () => events.ValidatePrincipal(context);
        await validation.Should().ThrowAsync<OperationCanceledException>();

        authentication.VerifyNoOtherCalls();
        context.Principal!.Identity!.IsAuthenticated.Should().BeTrue();
        var stored = (await fixture.NewStore().RetrieveAsync(key))!;
        stored.Principal.FindFirstValue(PortalAuthClaims.RefreshToken).Should().Be("refresh-1");
        (await fixture.Validator().ValidateAndRefreshAsync(stored.Principal)).IsValid.Should().BeTrue();
    }

    [Test]
    public async Task TicketLookup_CiphertextCopiedFromAnotherSession_RejectsSubstitution()
    {
        var fixture = new SessionFixture();
        var first = fixture.CreateTicket();
        var second = fixture.CreateTicket();
        var identity = (ClaimsIdentity)second.Principal.Identity!;
        identity.RemoveClaim(identity.FindFirst(PortalAuthClaims.SessionId)!);
        identity.AddClaim(new Claim(PortalAuthClaims.SessionId, Guid.NewGuid().ToString()));
        var firstKey = await fixture.Store.StoreAsync(first);
        var secondKey = await fixture.Store.StoreAsync(second);
        var otherPayload = await fixture.Cache.GetAsync($"portal:authentication:session:{secondKey}");

        await fixture.Cache.SetAsync($"portal:authentication:session:{firstKey}", otherPayload!);

        (await fixture.Store.RetrieveAsync(firstKey)).Should().BeNull();
        (await fixture.Store.ReadPrincipalAsync(first.Principal)).Should().BeNull();
        (await fixture.Store.RetrieveAsync(secondKey)).Should().NotBeNull();
    }

    [TestCase(PortalAuthClaims.TenantId)]
    [TestCase(PortalAuthClaims.CredentialId)]
    [TestCase(PortalAuthClaims.RoleTypeId)]
    public async Task MismatchedBindings_CannotReadOrRenewAnotherTicket(string claimType)
    {
        var fixture = new SessionFixture();
        var ticket = fixture.CreateTicket();
        var key = await fixture.Store.StoreAsync(ticket);
        var identity = (ClaimsIdentity)ticket.Principal.Identity!;
        identity.RemoveClaim(identity.FindFirst(claimType)!);
        identity.AddClaim(new Claim(claimType, Guid.NewGuid().ToString()));
        var expiry = ticket.Properties.ExpiresUtc;
        ticket.Properties.ExpiresUtc = expiry!.Value.AddDays(1);

        (await fixture.Store.ReadPrincipalAsync(ticket.Principal)).Should().BeNull();
        (await fixture.Validator().ValidateAndRefreshAsync(ticket.Principal)).IsValid.Should().BeFalse();
        await fixture.Store.RenewAsync(key, ticket);
        (await fixture.Store.RetrieveAsync(key))!.Properties.ExpiresUtc.Should().Be(expiry);
        fixture.RefreshCalls.Should().Be(0);
    }

    [Test]
    public async Task IndependentSessions_ForSameAccount_DoNotShareTokensOrLogout()
    {
        var fixture = new SessionFixture();
        var first = fixture.CreateTicket();
        var second = fixture.CreateTicket();
        var identity = (ClaimsIdentity)second.Principal.Identity!;
        identity.RemoveClaim(identity.FindFirst(PortalAuthClaims.SessionId)!);
        identity.AddClaim(new Claim(PortalAuthClaims.SessionId, Guid.NewGuid().ToString()));
        var firstKey = await fixture.Store.StoreAsync(first);
        var secondKey = await fixture.Store.StoreAsync(second);
        fixture.Clock.Advance(TimeSpan.FromMinutes(30));
        await fixture.Validator().ValidateAndRefreshAsync(first.Principal);

        (await fixture.Store.RetrieveAsync(secondKey))!.Principal.FindFirstValue(PortalAuthClaims.RefreshToken)
            .Should().Be("refresh-0");
        await fixture.Store.RemoveAsync(firstKey);
        (await fixture.Store.RetrieveAsync(firstKey)).Should().BeNull();
        (await fixture.Store.RetrieveAsync(secondKey)).Should().NotBeNull();
    }

    [TestCase(401)]
    [TestCase(403)]
    [TestCase(503)]
    public async Task StoredTicket_BackendRejectsSession_DoesNotBypassValidation(int statusCode)
    {
        var fixture = new SessionFixture();
        var ticket = fixture.CreateTicket();
        await fixture.Store.StoreAsync(ticket);
        fixture.RejectionStatus = statusCode;

        (await fixture.Validator().ValidateAndRefreshAsync(ticket.Principal)).IsValid.Should().BeFalse();
    }

    [Test]
    public async Task TicketStorage_ProtectsCredentialsInSharedCache()
    {
        var fixture = new SessionFixture();
        var ticket = fixture.CreateTicket();
        var key = await fixture.Store.StoreAsync(ticket);

        var payload = await fixture.Cache.GetAsync($"portal:authentication:session:{key}");

        System.Text.Encoding.UTF8.GetString(payload!).Should().NotContain("refresh-0");
        payload![0] ^= 0xff;
        await fixture.Cache.SetAsync($"portal:authentication:session:{key}", payload);
        (await fixture.Store.RetrieveAsync(key)).Should().BeNull();
    }

    private static ClaimsPrincipal ClonePrincipal(ClaimsPrincipal principal) =>
        new(principal.Identities.Select(identity => identity.Clone()));

    private sealed class SessionFixture
    {
        private readonly Guid _tenant = Guid.NewGuid();
        private readonly Guid _credential = Guid.NewGuid();
        private readonly Guid _session = Guid.NewGuid();
        private readonly Guid _role = Guid.NewGuid();
        private readonly Mock<IActorIdentityProvider> _actor = new();
        private readonly Mock<IIdentityServerServiceWrapper> _identity = new();
        private readonly IDataProtectionProvider _protection;
        public TestClock Clock { get; } = new();
        public IDistributedCache Cache { get; }
        public PortalAuthenticationTicketStore Store { get; }
        public int RefreshCalls { get; private set; }
        public int? RejectionStatus { get; set; }
        public Func<Task>? BeforeRefresh { get; set; }

        public SessionFixture(IDistributedCache? cache = null, IDataProtectionProvider? protection = null)
        {
            Cache = cache ?? new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
            _protection = protection ?? new EphemeralDataProtectionProvider();
            Store = NewStore();
            _actor.Setup(actor => actor.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string token, CancellationToken ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    var expiry = new DateTimeOffset(new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo);
                    return Task.FromResult(RejectionStatus is { } status
                        ? ActorIdentityValidationResult.Failure("Session rejected", status)
                        : expiry <= Clock.GetUtcNow()
                            ? ActorIdentityValidationResult.Failure("Expired", 401)
                            : ActorIdentityValidationResult.Success(new TrustedActorIdentity(
                                _credential, Guid.NewGuid(), _tenant, _session,
                                new HashSet<string>(), new HashSet<string>(), "generation", expiry)));
                });
            _identity.Setup(identity => identity.RefreshToken(It.IsAny<RefreshTokenRequest>(), It.IsAny<CancellationToken>()))
                .Returns(async (RefreshTokenRequest request, CancellationToken ct) =>
                {
                    if (BeforeRefresh is not null) await BeforeRefresh();
                    ct.ThrowIfCancellationRequested();
                    if (RejectionStatus is not null || request.RefreshToken != $"refresh-{RefreshCalls}")
                        return new QueryResponse<RefreshTokenResponse> { HttpStatusCode = HttpStatusCode.Unauthorized };
                    RefreshCalls++;
                    return new QueryResponse<RefreshTokenResponse>
                    {
                        HttpStatusCode = HttpStatusCode.OK,
                        Response = new RefreshTokenResponse
                        {
                            SessionId = _session, AccessToken = Token(), RefreshToken = $"refresh-{RefreshCalls}", ExpiresIn = 1800
                        }
                    };
                });
        }

        public PortalAuthenticationTicketStore NewStore() => new(Cache, _protection, Clock);

        public AuthenticationTicket CreateTicket() => new(
            new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(PortalAuthClaims.TenantId, _tenant.ToString()),
                new Claim(PortalAuthClaims.CredentialId, _credential.ToString()),
                new Claim(PortalAuthClaims.SessionId, _session.ToString()),
                new Claim(PortalAuthClaims.RoleTypeId, _role.ToString()),
                new Claim(PortalAuthClaims.ActorAccessToken, Token()),
                new Claim(PortalAuthClaims.RefreshToken, "refresh-0")
            ], PortalAuthDefaults.AuthenticationScheme)),
            new AuthenticationProperties { ExpiresUtc = Clock.GetUtcNow().AddHours(12) },
            PortalAuthDefaults.AuthenticationScheme);

        public PortalIdentitySessionValidator Validator(
            PortalAuthenticationTicketStore? store = null, PortalActorTokenRefreshCoordinator? coordinator = null) => new(
            _actor.Object, _identity.Object, new PortalActorAccessTokenScope(), coordinator ?? new PortalActorTokenRefreshCoordinator(),
            store ?? Store, Clock, NullLogger<PortalIdentitySessionValidator>.Instance);

        private string Token() => new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(expires: Clock.GetUtcNow().AddMinutes(30).UtcDateTime));
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}
