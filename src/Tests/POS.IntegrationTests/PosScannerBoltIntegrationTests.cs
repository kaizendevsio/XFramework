using System.Net;
using System.Net.Sockets;
using Bolt.Client;
using Bolt.Hub.Extensions;
using Bolt.Hub.Services;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using POS.Api.Services;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Contracts.Responses;
using POS.Integration.Drivers;
using XFramework.Core.Extensions;
using XFramework.Core.Middlewares;
using XFramework.Core.Patterns;
using XFramework.Core.RateLimiting;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Extensions;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Extensions;
using XFramework.Integration.Extensions;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Security;
using XFramework.TestInfrastructure;

namespace POS.IntegrationTests;

// Real generated handlers + wrapper + loopback Bolt. No deployed credentials, DB or financial calls.
[TestFixture]
[NonParallelizable]
[Category("Kind:Integration")]
[Category("Module:POS")]
[Category("Area:Scanner")]
public sealed class PosScannerBoltIntegrationTests
{
    private readonly Guid tenant = Guid.NewGuid();
    private readonly Guid cashier = Guid.NewGuid();
    private readonly Guid desktopSession = Guid.NewGuid();
    private readonly Guid phoneSession = Guid.NewGuid();
    private ManualClock clock = null!;
    private PosScannerPairingStore store = null!;
    private TestBoltTransportAuthority authority = null!;
    private TestInvocationIdentityOptions identity = null!;
    private readonly List<WebApplication> apps = [];
    private IServiceScope scope = null!;
    private IPOSServiceWrapper wrapper = null!;
    private string hubUrl = "";
    private bool enabled = true;

    [OneTimeSetUp]
    public async Task Start()
    {
        hubUrl = FreeUrl();
        authority = new(hubUrl);
        identity = new(Token(desktopSession), "scanner-tests-service-token", XFrameworkServiceNames.Portal,
            tenant, cashier, Guid.NewGuid(), desktopSession);
        clock = new();
        store = new(clock);

        var hub = Builder("Bolt.ScannerTests", hubUrl);
        authority.Configure(hub);
        hub.Services.InstallServicesInAssembly<Bolt.Hub.Installers.BoltInstaller>(hub.Configuration, hub.Environment);
        hub.Services.InstallSwagger(hub.Configuration);
        hub.Services.InstallOData(hub.Configuration);
        hub.Services.InstallJwt(hub.Configuration);
        hub.Services.InstallStandardServices<Bolt.Hub.Installers.BoltInstaller>(hub.Configuration);
        hub.Services.InstallRuntimeServices(hub.Configuration);
        hub.Services.AddTestInvocationClient(identity);
        hub.Services.AddSingleton(Mock.Of<IBoltServiceDiscoveryRegistry>());
        var hubApp = hub.Build();
        apps.Add(hubApp);
        authority.MapEndpoints(hubApp);
        hubApp.UseCorrelationId();
        hubApp.UseAppServices();
        await hubApp.StartAsync();

        var pos = Builder(XFrameworkServiceNames.Pos, FreeUrl());
        pos.Services.InstallStandardServices<PosScannerService>(pos.Configuration);
        pos.Services.RemoveAll<IJwtService>();
        pos.Services.AddSingleton(Mock.Of<IJwtService>());
        pos.Services.AddXFrameworkBoltClient(pos.Configuration, autoConnect: false);
        pos.Services.AddTestInvocationServer(identity);
        pos.Services.AddSingleton(authority.CreateTokenProvider(XFrameworkServiceNames.Pos));
        pos.Services.AddSingleton<TimeProvider>(clock);
        pos.Services.AddSingleton(store);
        pos.Services.AddSingleton<IDistributedSecurityRateLimiter, AllowedScannerClaimLimiter>();
        pos.Services.AddScoped<IPosRequestContextResolver, PosRequestContextResolver>();
        pos.Services.AddScoped<PosScannerService>();
        pos.Services.AddScoped(_ => new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options,
            new HttpContextAccessor(), new ConfigurationBuilder().Build(), new TestEffectiveTenantContextAccessor(tenant)));
        pos.Services.AddValidatorsFromAssemblyContaining<PosScannerService>();
        var features = new Mock<ITenantModuleFeatureService>();
        features.Setup(f => f.EnsureEnabledAsync(It.IsAny<Guid>(), "pos", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => enabled ? Result.Success() : Result.Forbidden("Feature disabled"));
        pos.Services.AddSingleton(features.Object);
        var routeGate = new Mock<ITrustedInvocationFeatureGate>();
        routeGate.Setup(g => g.EnsureAllowedAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        pos.Services.AddSingleton(routeGate.Object);
        var posApp = pos.Build();
        apps.Add(posApp);
        POS.Api.Generated.BoltHandlerRegistry.RegisterAll(posApp.Services.GetRequiredService<BoltClient>(),
            posApp.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ScannerTests"),
            posApp.Services.GetRequiredService<IServiceScopeFactory>());
        await posApp.StartAsync();

        var portal = Builder(XFrameworkServiceNames.Portal, FreeUrl());
        portal.Services.InstallStandardServices<PosScannerBoltIntegrationTests>(portal.Configuration);
        portal.Services.RemoveAll<IJwtService>();
        portal.Services.AddSingleton(Mock.Of<IJwtService>());
        portal.Services.AddXFrameworkBoltClient(portal.Configuration, autoConnect: false);
        portal.Services.AddSingleton(authority.CreateTokenProvider(XFrameworkServiceNames.Portal));
        portal.Services.AddTestInvocationClient(identity);
        portal.Services.AddPOSWrapperServices();
        var portalApp = portal.Build();
        apps.Add(portalApp);
        await portalApp.StartAsync();
        using var connectionTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await posApp.Services.GetRequiredService<BoltClient>().ConnectWithRetryAsync(connectionTimeout.Token);
        await portalApp.Services.GetRequiredService<BoltClient>().ConnectWithRetryAsync(connectionTimeout.Token);
        scope = portalApp.Services.CreateScope();
        wrapper = scope.ServiceProvider.GetRequiredService<IPOSServiceWrapper>();
    }

    [OneTimeTearDown]
    public async Task Stop()
    {
        scope?.Dispose();
        foreach (var app in apps.AsEnumerable().Reverse()) { await app.StopAsync(); await app.DisposeAsync(); }
        authority?.Dispose();
    }

    [SetUp]
    public void Reset() { enabled = true; clock.Reset(); store = apps[1].Services.GetRequiredService<PosScannerPairingStore>(); }

    [Test]
    public async Task Scanner_IndependentLoginPairSendReplayPollAckRevoke_ThroughGeneratedWrappers()
    {
        var pairing = Pairing();
        PosScannerPhoneResponse phone;
        using (TestInvocationActorTokenScope.Push(Token(phoneSession)))
        {
            var claim = await wrapper.ClaimPosScannerPairing(new() { Challenge = pairing.Challenge });
            claim.IsSuccess.Should().BeTrue(claim.Message);
            phone = claim.Response!;
            var send = new SendPosScannerCodeRequest { PairingId = phone.PairingId, PhoneKey = phone.PhoneKey, Sequence = 1, Code = "012345678905" };
            (await wrapper.SendPosScannerCode(send)).IsSuccess.Should().BeTrue();
            (await wrapper.SendPosScannerCode(send)).Response!.Duplicate.Should().BeTrue();
            (await wrapper.SendPosScannerCode(send with { Code = "other" })).HttpStatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        var poll = new PollPosScannerCodesRequest { PairingId = pairing.PairingId, DesktopKey = pairing.DesktopKey };
        var received = await wrapper.PollPosScannerCodes(poll);
        received.IsSuccess.Should().BeTrue(received.Message);
        received.Response!.Codes.Should().ContainSingle(c => c.Code == "012345678905");
        (await wrapper.PollPosScannerCodes(poll)).Response!.Codes.Should().ContainSingle();
        (await wrapper.PollPosScannerCodes(poll with { AcknowledgedSequence = 1 })).Response!.Codes.Should().BeEmpty();
        (await wrapper.RevokePosScannerPairing(new() { PairingId = pairing.PairingId, DesktopKey = pairing.DesktopKey })).IsSuccess.Should().BeTrue();
        using (TestInvocationActorTokenScope.Push(Token(phoneSession)))
        {
            (await wrapper.GetPosScannerStatus(new() { PairingId = phone.PairingId, PhoneKey = phone.PhoneKey })).HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await wrapper.SendPosScannerCode(new() { PairingId = phone.PairingId, PhoneKey = phone.PhoneKey, Sequence = 2, Code = "SKU-2" })).HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Test]
    public async Task Claim_DeniedActorTenantCapabilitySameSessionAndReusedQr_ThroughGeneratedWrappers()
    {
        var pairing = Pairing();
        (await wrapper.ClaimPosScannerPairing(new() { Challenge = pairing.Challenge })).HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        foreach (var token in new[]
        {
            Token(phoneSession, actor: Guid.NewGuid()), Token(phoneSession, tenantId: Guid.NewGuid()),
            Token(phoneSession, capabilities: []), "invalid-actor-token"
        })
        {
            using var actor = TestInvocationActorTokenScope.Push(token);
            var denied = await wrapper.ClaimPosScannerPairing(new() { Challenge = pairing.Challenge });
            denied.IsSuccess.Should().BeFalse();
            ((int)denied.HttpStatusCode).Should().BeOneOf(401, 403);
        }
        using (TestInvocationActorTokenScope.Push(Token(phoneSession)))
        {
            var mismatch = await wrapper.ClaimPosScannerPairing(new()
            { Challenge = pairing.Challenge, Metadata = new RequestMetadata { RequestedTenantId = Guid.NewGuid() } });
            mismatch.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await wrapper.ClaimPosScannerPairing(new() { Challenge = pairing.Challenge })).IsSuccess.Should().BeTrue();
            (await wrapper.ClaimPosScannerPairing(new() { Challenge = pairing.Challenge })).HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Test]
    public async Task Poll_NewSaleFlush_RoundTripsAcknowledgementWithoutReplacingPairing()
    {
        var pairing = Pairing();
        PosScannerPhoneResponse phone;
        using (TestInvocationActorTokenScope.Push(Token(phoneSession)))
        {
            phone = (await wrapper.ClaimPosScannerPairing(new() { Challenge = pairing.Challenge })).Response!;
            (await wrapper.SendPosScannerCode(new() { PairingId = phone.PairingId, PhoneKey = phone.PhoneKey, Sequence = 1, Code = "OLD-SALE" })).IsSuccess.Should().BeTrue();
        }
        var poll = new PollPosScannerCodesRequest { PairingId = pairing.PairingId, DesktopKey = pairing.DesktopKey, DiscardPendingCodes = true };
        var reset = await wrapper.PollPosScannerCodes(poll);
        reset.IsSuccess.Should().BeTrue(reset.Message);
        reset.Response!.AcknowledgedSequence.Should().Be(1);
        reset.Response.Codes.Should().BeEmpty();
        reset.Response.IsPaired.Should().BeTrue();
        using (TestInvocationActorTokenScope.Push(Token(phoneSession)))
            (await wrapper.SendPosScannerCode(new() { PairingId = phone.PairingId, PhoneKey = phone.PhoneKey, Sequence = 2, Code = "NEW-SALE" })).IsSuccess.Should().BeTrue();
        (await wrapper.PollPosScannerCodes(poll with { DiscardPendingCodes = false, AcknowledgedSequence = 1 })).Response!.Codes.Should().ContainSingle(c => c.Code == "NEW-SALE");
    }

    [Test]
    public async Task Claim_ExpiredOrFeatureDisabled_ThroughGeneratedWrappers()
    {
        var pairing = Pairing();
        enabled = false;
        using (TestInvocationActorTokenScope.Push(Token(phoneSession)))
            (await wrapper.ClaimPosScannerPairing(new() { Challenge = pairing.Challenge })).HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        enabled = true;
        clock.Advance(TimeSpan.FromMinutes(2));
        using (TestInvocationActorTokenScope.Push(Token(phoneSession)))
            (await wrapper.ClaimPosScannerPairing(new() { Challenge = pairing.Challenge })).HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Send_PhoneSessionAndDesktopTabIsolation_ThroughGeneratedWrappers()
    {
        var pairing = Pairing();
        var anotherTab = Pairing();
        PosScannerPhoneResponse phone;
        using (TestInvocationActorTokenScope.Push(Token(phoneSession)))
            phone = (await wrapper.ClaimPosScannerPairing(new() { Challenge = pairing.Challenge })).Response!;
        (await wrapper.SendPosScannerCode(new() { PairingId = phone.PairingId, PhoneKey = phone.PhoneKey, Sequence = 1, Code = "SKU" }))
            .HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await wrapper.PollPosScannerCodes(new() { PairingId = pairing.PairingId, DesktopKey = anotherTab.DesktopKey }))
            .HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private PosScannerPairingResponse Pairing() =>
        store.Create(new(tenant, cashier, desktopSession), Guid.NewGuid(), "Test register").Data!;

    [Test]
    public async Task ShortCode_ManualContractValidationAndSingleUse_ThroughGeneratedWrappers()
    {
        var pairing = Pairing();
        pairing.PairingCode.Should().MatchRegex("^[0-9]{6}$");
        var roundtrip = MemoryPack.MemoryPackSerializer.Deserialize<PosScannerPairingResponse>(
            MemoryPack.MemoryPackSerializer.Serialize(pairing));
        roundtrip!.PairingCode.Should().Be(pairing.PairingCode);
        using var phone = TestInvocationActorTokenScope.Push(Token(phoneSession));
        foreach (var request in new[]
        {
            new ClaimPosScannerPairingRequest { PairingCode = "12345" },
            new ClaimPosScannerPairingRequest { PairingCode = "ABCDEF" },
            new ClaimPosScannerPairingRequest { PairingCode = pairing.PairingCode, Challenge = pairing.Challenge }
        })
            (await wrapper.ClaimPosScannerPairing(request)).HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        var claim = await wrapper.ClaimPosScannerPairing(new() {PairingCode=pairing.PairingCode});
        claim.IsSuccess.Should().BeTrue(claim.Message);
        claim.Response!.PairingId.Should().Be(pairing.PairingId);
        (await wrapper.ClaimPosScannerPairing(new() {PairingCode=pairing.PairingCode})).HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await wrapper.ClaimPosScannerPairing(new() {Challenge=pairing.Challenge})).HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed class AllowedScannerClaimLimiter : IDistributedSecurityRateLimiter
    {
        public ValueTask<DistributedSecurityRateLimitDecision> AcquireAsync(StrictSecurityRateLimitPolicy policy,
            string clientKey, CancellationToken cancellationToken) => ValueTask.FromResult(DistributedSecurityRateLimitDecision.Allowed);
    }

    [Test]
    public async Task Scanner_TrustedDelegatedTenant_SameCredentialOnly_WithTargetMetadataOnBothDevices()
    {
        var target = Guid.NewGuid();
        var pairing = store.Create(new(target, cashier, desktopSession), Guid.NewGuid(), "Delegated register").Data!;
        var capabilities = new[] { PosAuthorizationCapabilities.SalesView, PosAuthorizationCapabilities.SalesCreate,
            XFrameworkActorCapabilities.IdentityTenantsManage };
        PosScannerPhoneResponse phone;
        using (TestInvocationActorTokenScope.Push(Token(phoneSession)))
            (await wrapper.ClaimPosScannerPairing(new()
            { Challenge = pairing.Challenge, Metadata = new() { RequestedTenantId = target } }))
                .HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        using (TestInvocationActorTokenScope.Push(Token(phoneSession, capabilities: capabilities)))
        {
            phone = (await wrapper.ClaimPosScannerPairing(new()
            { Challenge = pairing.Challenge, Metadata = new() { RequestedTenantId = target } })).Response!;
            phone.Should().NotBeNull();
            var send = new SendPosScannerCodeRequest
            { PairingId = phone.PairingId, PhoneKey = phone.PhoneKey, Sequence = 1, Code = "SKU-1", Metadata = new() { RequestedTenantId = target } };
            (await wrapper.SendPosScannerCode(send)).IsSuccess.Should().BeTrue();
            (await wrapper.SendPosScannerCode(send with { Sequence = 2 })).Response!.Duplicate.Should().BeFalse();
            (await wrapper.SendPosScannerCode(send with { Sequence = 3, Metadata = new() { RequestedTenantId = tenant } }))
                .HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        using (TestInvocationActorTokenScope.Push(Token(desktopSession, capabilities: capabilities)))
        {
            var poll = new PollPosScannerCodesRequest
            { PairingId = pairing.PairingId, DesktopKey = pairing.DesktopKey, Metadata = new() { RequestedTenantId = target } };
            (await wrapper.PollPosScannerCodes(poll with { PauseDelivery = true, AcknowledgedSequence = 2 })).Response!.Codes.Should().BeEmpty();
            (await wrapper.PollPosScannerCodes(poll)).Response!.Codes.Select(c => c.Sequence).Should().Equal(1,2);
            (await wrapper.RevokePosScannerPairing(new()
            { PairingId = pairing.PairingId, DesktopKey = pairing.DesktopKey, Metadata = new() { RequestedTenantId = target } })).IsSuccess.Should().BeTrue();
        }
    }
    private string Token(Guid session, Guid? actor = null, Guid? tenantId = null, string[]? capabilities = null) =>
        TestInvocationIdentityExtensions.CreateTestActorToken(tenantId ?? tenant, actor ?? cashier, Guid.NewGuid(),
            session, ["Cashier"], capabilities ?? [PosAuthorizationCapabilities.SalesView, PosAuthorizationCapabilities.SalesCreate]);

    private WebApplicationBuilder Builder(string name, string url)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BoltConfiguration:ClientName"] = name,
            ["BoltConfiguration:ClientGuid"] = Guid.NewGuid().ToString(),
            ["BoltConfiguration:ServerUrls:0"] = $"{hubUrl}/bolt/ws",
            ["ServiceIdentity:ClientId"] = name, ["ServiceIdentity:Authority"] = hubUrl,
            ["ServiceIdentity:AllowInsecureHttp"] = "true", ["ServiceIdentity:GenerationId"] = "scanner-tests-g1",
            ["ServiceIdentity:ClientSecret"] = "scanner-isolated-test-secret-not-a-deployed-credential",
            ["ServiceIdentity:DefaultScopes:0"] = XFrameworkServiceScopes.BoltService,
            ["Tenant:DefaultId"] = tenant.ToString(), ["Kestrel:Endpoints:Http:Url"] = url,
            ["urls"] = url, ["Logging:LogLevel:Default"] = "Warning"
        });
        builder.WebHost.UseUrls(url);
        return builder;
    }

    private static string FreeUrl()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan value) => now += value;
        public void Reset() => now = DateTimeOffset.UtcNow;
    }
}
