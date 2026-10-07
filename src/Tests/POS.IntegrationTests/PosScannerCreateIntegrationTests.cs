using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using POS.Api.Services;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Requests;
using Testcontainers.PostgreSql;
using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;
using XFramework.TestInfrastructure;

namespace POS.IntegrationTests;

[TestFixture]
[NonParallelizable]
[Category(TestCategories.Integration)]
[Category(TestCategories.POS)]
[Category("Area:Scanner")]
public sealed class PosScannerCreateIntegrationTests
{
    private PostgreSqlContainer postgres = null!;
    private DbContextOptions<AppDbContext> options = null!;

    [OneTimeSetUp]
    public async Task StartDatabase()
    {
        postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("pos_scanner_create")
            .WithUsername("qa").WithPassword("isolated_test_only").Build();
        await postgres.StartAsync();
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(PosRegister).TypeHandle);
        options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(postgres.GetConnectionString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;
        await using var db = SeedContext(Guid.NewGuid());
        await db.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public async Task StopDatabase()
    {
        if (postgres is not null) await postgres.DisposeAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CreateAsync_EnabledRegisterInEffectiveTenant_CreatesClaimablePairingWithoutChangingRegister(bool delegated)
    {
        var tenant = Guid.NewGuid();
        var register = Register(tenant);
        await Seed(register);
        var actor = Actor(delegated ? Guid.NewGuid() : tenant, delegated);
        var invocation = new Invocation { Current = new(actor, null, tenant, tenant, Guid.NewGuid()) };
        await using var db = Context(invocation);
        var features = EnabledFeatures(tenant);
        var service = Service(db, invocation, features);

        var result = await service.CreateAsync(new CreatePosScannerPairingRequest
        { RegisterId = register.Id, Metadata = new() { RequestedTenantId = tenant } }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.StatusCode.Should().Be(200);
        result.Data.Should().NotBeNull();
        result.Data!.PairingId.Should().NotBeEmpty();
        result.Data.Challenge.Should().HaveLength(64);
        result.Data.DesktopKey.Should().HaveLength(64);
        features.VerifyAll();
        var phoneActor = new TrustedActorIdentity(actor.CredentialId, actor.IdentityId, actor.TenantId,
            Guid.NewGuid(), actor.Roles, actor.Capabilities, actor.GenerationId, actor.ExpiresAtUtc);
        invocation.Current = invocation.Current! with { Actor = phoneActor };
        var claim = await service.ClaimAsync(new ClaimPosScannerPairingRequest
        { Challenge = result.Data.Challenge, Metadata = new() { RequestedTenantId = tenant } }, CancellationToken.None);
        claim.IsSuccess.Should().BeTrue(claim.Message);
        claim.Data!.PairingId.Should().Be(result.Data.PairingId);
        claim.Data.RegisterName.Should().Be(register.Name);
        await AssertRegisterUnchanged(register);
    }

    [TestCase("disabled")]
    [TestCase("wrong-tenant")]
    [TestCase("deleted")]
    public async Task CreateAsync_UnavailableRegister_ReturnsNotFoundWithoutChangingRegister(string scenario)
    {
        var tenant = Guid.NewGuid();
        var register = Register(scenario == "wrong-tenant" ? Guid.NewGuid() : tenant);
        register.IsEnabled = scenario != "disabled";
        register.IsDeleted = scenario == "deleted";
        await Seed(register);
        var invocation = new Invocation { Current = new(Actor(tenant), null, tenant, tenant, Guid.NewGuid()) };
        await using var db = Context(invocation);
        var features = EnabledFeatures(tenant);
        var service = Service(db, invocation, features);

        var result = await service.CreateAsync(new CreatePosScannerPairingRequest
        { RegisterId = register.Id, Metadata = new() { RequestedTenantId = tenant } }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
        result.Message.Should().Be("Enabled POS register was not found");
        result.Data.Should().BeNull();
        features.VerifyAll();
        await AssertRegisterUnchanged(register);
    }

    private AppDbContext Context(Invocation invocation) => new(options, new HttpContextAccessor(),
        new ConfigurationBuilder().Build(), invocation);

    private AppDbContext SeedContext(Guid tenant) => new(options, new HttpContextAccessor(),
        new ConfigurationBuilder().Build(), new TestEffectiveTenantContextAccessor(tenant));

    private async Task Seed(PosRegister register)
    {
        await using var db = SeedContext(register.TenantId);
        db.Add(register);
        await db.SaveChangesAsync();
    }

    private async Task AssertRegisterUnchanged(PosRegister expected)
    {
        await using var db = SeedContext(expected.TenantId);
        // Fixture-only inspection includes the deliberately soft-deleted denial case.
        var actual = await db.Set<PosRegister>().IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(p => p.Id == expected.Id);
        actual.TenantId.Should().Be(expected.TenantId);
        actual.Name.Should().Be(expected.Name);
        actual.IsEnabled.Should().Be(expected.IsEnabled);
        actual.IsDeleted.Should().Be(expected.IsDeleted);
        actual.ConcurrencyStamp.Should().Be(expected.ConcurrencyStamp);
    }

    private static PosScannerService Service(AppDbContext db, Invocation invocation,
        Mock<ITenantModuleFeatureService> features) => new(db, new PosRequestContextResolver(invocation),
        invocation, features.Object, new PosScannerPairingStore(TimeProvider.System), TimeProvider.System);

    private static Mock<ITenantModuleFeatureService> EnabledFeatures(Guid tenant)
    {
        var features = new Mock<ITenantModuleFeatureService>(MockBehavior.Strict);
        foreach (var subFeature in new string?[] { null, "registers", "sales" })
            features.Setup(f => f.EnsureEnabledAsync(tenant, "pos", subFeature, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success());
        return features;
    }

    private static TrustedActorIdentity Actor(Guid tenant, bool delegated = false) => new(
        Guid.NewGuid(), Guid.NewGuid(), tenant, Guid.NewGuid(), new HashSet<string> { "Cashier" },
        new HashSet<string>(delegated
            ? [PosAuthorizationCapabilities.SalesView, PosAuthorizationCapabilities.SalesCreate, XFrameworkActorCapabilities.IdentityTenantsManage]
            : [PosAuthorizationCapabilities.SalesView, PosAuthorizationCapabilities.SalesCreate]),
        "scanner-create-tests", DateTimeOffset.UtcNow.AddMinutes(10));

    private static PosRegister Register(Guid tenant) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenant, Name = "Isolated scanner register",
        MerchantCredentialId = Guid.NewGuid(), CashDrawerWalletId = Guid.NewGuid(),
        WalletTypeId = Guid.NewGuid(), CurrencyId = Guid.NewGuid(),
        DefaultWarehouseId = Guid.NewGuid(), DefaultLocationId = Guid.NewGuid(),
        IsEnabled = true, CreatedAt = DateTime.UtcNow, ConcurrencyStamp = Guid.NewGuid()
    };

    private sealed class Invocation : ITrustedInvocationContextAccessor
    {
        public TrustedInvocationContext? Current { get; set; }
    }
}
