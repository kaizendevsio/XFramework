using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using POS.Api.Services;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;
using XFramework.TestInfrastructure;

namespace POS.Api.Tests;

[TestFixture]
[Category(TestCategories.POS)]
public sealed class PosScannerServiceSecurityTests
{
    private AppDbContext db = null!;
    private Accessor accessor = null!;
    private Features features = null!;
    private PosScannerService service = null!;
    private Guid tenant;
    private TrustedActorIdentity actor = null!;

    [SetUp]
    public void Setup()
    {
        tenant = Guid.NewGuid();
        actor = Actor();
        accessor = new() { Current = new(actor, null, tenant, null, Guid.NewGuid()) };
        features = new();
        db = new(new DbContextOptionsBuilder<AppDbContext>().Options, new HttpContextAccessor(),
            new ConfigurationBuilder().Build(), new TestEffectiveTenantContextAccessor(tenant));
        service = new(db, new PosRequestContextResolver(accessor), accessor, features,
            new PosScannerPairingStore(TimeProvider.System), TimeProvider.System);
    }

    [TearDown]
    public void Stop() => db.Dispose();

    [Test]
    public async Task Create_ClientTenantMismatch_IsDeniedBeforeRegisterQuery()
    {
        var result = await service.CreateAsync(new()
        {
            RegisterId = Guid.NewGuid(), Metadata = new() { RequestedTenantId = Guid.NewGuid() }
        }, CancellationToken.None);
        result.StatusCode.Should().Be(403);
        features.Calls.Should().BeEmpty();
    }

    [Test]
    public async Task Create_ServiceOnlyEvenPrivileged_IsDeniedBeforeRegisterQuery()
    {
        accessor.Current = new(null, new TrustedServiceIdentity(XFrameworkServiceNames.Portal,
            XFrameworkServiceNames.Pos, new HashSet<string>(), "tests"), tenant, null, Guid.NewGuid());
        var result = await service.CreateAsync(new() { RegisterId = Guid.NewGuid() }, CancellationToken.None);
        result.StatusCode.Should().Be(401);
    }

    [Test]
    public async Task Claim_ExpiredActorOrMissingPermission_IsDeniedBeforePairing()
    {
        accessor.Current = accessor.Current! with { Actor = Actor(expired: true) };
        (await service.ClaimAsync(new() { Challenge = new string('A', 64) }, CancellationToken.None)).StatusCode.Should().Be(401);
        accessor.Current = accessor.Current with { Actor = Actor(capabilities: []) };
        (await service.ClaimAsync(new() { Challenge = new string('A', 64) }, CancellationToken.None)).StatusCode.Should().Be(403);
    }

    [Test]
    public async Task Claim_DelegatedTenantWithoutTenantManagementCapability_IsDenied()
    {
        accessor.Current = new(actor, null, Guid.NewGuid(), null, Guid.NewGuid());
        (await service.ClaimAsync(new(), CancellationToken.None)).StatusCode.Should().Be(403);
    }

    [Test]
    public async Task Claim_TrustedDelegatedTenant_KeepsSameCredentialAndUsesTargetFeatureGates()
    {
        var target = Guid.NewGuid();
        var store = new PosScannerPairingStore(TimeProvider.System);
        var pairing = store.Create(new(target, actor.CredentialId, actor.SessionId), Guid.NewGuid(), "Target register").Data!;
        var phone = new TrustedActorIdentity(actor.CredentialId, actor.IdentityId, actor.TenantId, Guid.NewGuid(),
            actor.Roles, new HashSet<string>(actor.Capabilities) { XFrameworkActorCapabilities.IdentityTenantsManage },
            actor.GenerationId, actor.ExpiresAtUtc);
        accessor.Current = new(phone, null, target, target, Guid.NewGuid());
        service = new(db, new PosRequestContextResolver(accessor), accessor, features, store, TimeProvider.System);
        var result = await service.ClaimAsync(new()
        { Challenge = pairing.Challenge, Metadata = new() { RequestedTenantId = target } }, CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Message);
        features.Tenants.Should().OnlyContain(t => t == target);
    }

    [TestCase(null)]
    [TestCase("registers")]
    [TestCase("sales")]
    public async Task Claim_DisabledRootOrDependency_IsDenied(string? disabled)
    {
        features.Disabled = disabled;
        features.Deny = true;
        var result = await service.ClaimAsync(new(), CancellationToken.None);
        result.StatusCode.Should().Be(403);
        features.Calls.Should().Contain(disabled);
    }

    private TrustedActorIdentity Actor(bool expired = false, string[]? capabilities = null) => new(
        Guid.NewGuid(), Guid.NewGuid(), tenant, Guid.NewGuid(),
        new HashSet<string> { "SuperAdmin" },
        new HashSet<string>(capabilities ?? [PosAuthorizationCapabilities.SalesView, PosAuthorizationCapabilities.SalesCreate]),
        "tests", DateTimeOffset.UtcNow.AddMinutes(expired ? -1 : 10));

    private sealed class Accessor : ITrustedInvocationContextAccessor
    {
        public TrustedInvocationContext? Current { get; set; }
    }

    private sealed class Features : ITenantModuleFeatureService
    {
        public string? Disabled { get; set; }
        public bool Deny { get; set; }
        public List<string?> Calls { get; } = [];
        public List<Guid> Tenants { get; } = [];
        public Task<Result> EnsureEnabledAsync(Guid tenantId, string moduleKey, string? subFeatureKey = null, CancellationToken ct = default)
        {
            Calls.Add(subFeatureKey);
            Tenants.Add(tenantId);
            return Task.FromResult(Deny && subFeatureKey == Disabled ? Result.Forbidden("Feature disabled") : Result.Success());
        }
        public Task<Result<bool>> IsEnabledAsync(Guid tenantId, string moduleKey, string? subFeatureKey = null, CancellationToken ct = default) =>
            Task.FromResult(Result<bool>.Success(true));
        public void Invalidate(Guid tenantId, string moduleKey, string? subFeatureKey = null) { }
    }
}
