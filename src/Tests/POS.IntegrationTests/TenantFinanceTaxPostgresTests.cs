using System.Net;
using IdentityServer.Api.Features.FinanceSettings;
using IdentityServer.Api.Features.PosSettings;
using IdentityServer.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using Inventario.Integration.Drivers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using POS.Api.Services;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Enums;
using Testcontainers.PostgreSql;
using XFramework.Core.Patterns;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Integration.Security;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Products;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Products;
using XFramework.TestInfrastructure;
using CurrencyType = Wallets.Domain.Shared.Contracts.CurrencyType;

namespace POS.IntegrationTests;

[TestFixture]
[NonParallelizable]
[Category("Kind:Integration")]
public sealed class TenantFinanceTaxPostgresTests
{
    private PostgreSqlContainer postgres = null!;
    private DbContextOptions<AppDbContext> options = null!;

    [OneTimeSetUp]
    public async Task Start()
    {
        postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(PosRegister).TypeHandle);
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(CurrencyType).TypeHandle);
        options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(postgres.GetConnectionString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)).Options;
        await using var db = Context(Guid.NewGuid());
        await db.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public async Task Stop() { if (postgres is not null) await postgres.DisposeAsync(); }

    [Test]
    public async Task FinanceSettings_PersistsRegistry_ValidatesCurrenciesAndConcurrency_AndPreservesTenantIsolation()
    {
        var tenant = Guid.NewGuid();
        await using var db = Context(tenant);
        var currency = Currency(tenant, "USD");
        db.Add(new Tenant { Id = tenant, TenantId = tenant, Name = "Finance test", Version = 1, IsEnabled = true });
        db.Add(currency);
        await db.SaveChangesAsync();
        var service = Finance(db, tenant);
        var fallback = await service.GetAsync(new(), CancellationToken.None);
        fallback.IsSuccess.Should().BeTrue(fallback.Message);
        fallback.Data!.Settings.SupportedCurrencyIds.Should().Contain(currency.Id);
        fallback.Data.ConcurrencyStamp.Should().BeEmpty();
        var settings = new TenantFinanceSettings { DefaultCurrencyId = currency.Id, SupportedCurrencyIds = [currency.Id], TaxEnabled = true, TaxRatePercent = 9 };
        var saved = await service.UpdateAsync(new() { Settings = settings }, CancellationToken.None);
        saved.IsSuccess.Should().BeTrue(saved.Message);
        (await db.Set<RegistryConfiguration>().SingleAsync()).Key.Should().Be(TenantFinanceSettings.RegistryKey);
        var stale = await service.UpdateAsync(new() { Settings = settings }, CancellationToken.None);
        stale.StatusCode.Should().Be(409);
        var invalid = await service.UpdateAsync(new()
        {
            ExpectedConcurrencyStamp = saved.Data!.ConcurrencyStamp,
            Settings = settings with { DefaultCurrencyId = Guid.NewGuid() }
        }, CancellationToken.None);
        invalid.StatusCode.Should().Be(400);
        await using var otherDb = Context(Guid.NewGuid());
        (await otherDb.Set<RegistryConfiguration>().CountAsync()).Should().Be(0);
        var read = await service.GetAsync(new(), CancellationToken.None);
        read.Data!.Settings.TaxRatePercent.Should().Be(9);
    }

    [Test]
    public async Task FinanceSettings_DisabledCurrency_AdminCanRepair_ConsumersFailClosed()
    {
        var tenant = Guid.NewGuid();
        await using var db = Context(tenant);
        var disabled = Currency(tenant, "USD");
        var active = Currency(tenant, "SGD");
        db.Add(new Tenant { Id = tenant, TenantId = tenant, Name = "Repair test", Version = 1, IsEnabled = true });
        db.AddRange(disabled, active);
        await db.SaveChangesAsync();
        var service = Finance(db, tenant);
        var saved = await service.UpdateAsync(new()
        { Settings = new() { DefaultCurrencyId = disabled.Id, SupportedCurrencyIds = [disabled.Id], TaxEnabled = true, TaxRatePercent = 10 } }, CancellationToken.None);
        saved.IsSuccess.Should().BeTrue(saved.Message);
        disabled.IsEnabled = false;
        await db.SaveChangesAsync();
        var consumer = await Finance(db, tenant, false).GetAsync(new(), CancellationToken.None);
        consumer.StatusCode.Should().Be(409);
        var editor = await service.GetAsync(new(), CancellationToken.None);
        editor.IsSuccess.Should().BeTrue(editor.Message);
        editor.Data!.IsUsable.Should().BeFalse();
        editor.Data.ConfigurationError.Should().NotBeNullOrEmpty();
        editor.Data.ConcurrencyStamp.Should().Be(saved.Data!.ConcurrencyStamp);
        editor.Data.Settings.DefaultCurrencyId.Should().Be(disabled.Id);
        editor.Data.SupportedCurrencies.Should().BeEmpty();
        editor.Data.AvailableCurrencies.Should().Contain(x => x.Id == active.Id).And.NotContain(x => x.Id == disabled.Id);
        var repaired = await service.UpdateAsync(new()
        {
            ExpectedConcurrencyStamp = editor.Data.ConcurrencyStamp,
            Settings = editor.Data.Settings with { DefaultCurrencyId = active.Id, SupportedCurrencyIds = [active.Id] }
        }, CancellationToken.None);
        repaired.IsSuccess.Should().BeTrue(repaired.Message);
        repaired.Data!.IsUsable.Should().BeTrue();
        (await Finance(db, tenant, false).GetAsync(new(), CancellationToken.None)).Data!.IsUsable.Should().BeTrue();
    }

    [Test]
    public async Task FinanceSettings_GlobalCurrencyIsVisible_NoCatalogIsUnconfigured_UnknownCurrencyIsRejected()
    {
        var tenant = Guid.NewGuid();
        await using var db = Context(tenant);
        // This isolated database may contain migration-seeded currencies; disable them for the empty-catalog assertion.
        await db.Set<CurrencyType>().IgnoreQueryFilters().ExecuteUpdateAsync(x => x.SetProperty(c => c.IsEnabled, false));
        var service = Finance(db, tenant);
        var empty = await service.GetAsync(new(), CancellationToken.None);
        empty.IsSuccess.Should().BeTrue(empty.Message);
        empty.Data!.SupportedCurrencies.Should().BeEmpty();
        empty.Data.IsUsable.Should().BeFalse();
        empty.Data.Settings.DefaultCurrencyId.Should().BeEmpty();
        var currency = Currency(tenant, "SGD");
        db.Add(currency);
        await db.SaveChangesAsync();
        // Fixture seeding only: normal runtime currency writes cannot create global rows.
        await db.Set<CurrencyType>().Where(x => x.Id == currency.Id).ExecuteUpdateAsync(x => x.SetProperty(c => c.TenantId, Guid.Empty));
        db.ChangeTracker.Clear();
        var read = await service.GetAsync(new(), CancellationToken.None);
        read.Data!.Settings.DefaultCurrencyId.Should().Be(currency.Id);
        var unknown = Guid.NewGuid();
        var invalid = await service.UpdateAsync(new()
        { Settings = new() { DefaultCurrencyId = unknown, SupportedCurrencyIds = [unknown] } }, CancellationToken.None);
        invalid.StatusCode.Should().Be(409);
    }

    [Test]
    public async Task CartAndCheckout_ComputeDiscountedTax_AndSaleReplayKeepsOriginalSnapshot()
    {
        var tenant = Guid.NewGuid();
        await using var db = Context(tenant);
        var register = new PosRegister
        {
            Id = Guid.NewGuid(), TenantId = tenant, Name = "Tax test", CurrencyId = Guid.NewGuid(),
            MerchantCredentialId = Guid.NewGuid(), CashDrawerWalletId = Guid.NewGuid(), WalletTypeId = Guid.NewGuid(),
            DefaultWarehouseId = Guid.NewGuid(), DefaultLocationId = Guid.NewGuid(), IsEnabled = true
        };
        db.Add(register);
        await db.SaveChangesAsync();
        var settings = new TenantFinanceSettings
        { DefaultCurrencyId = register.CurrencyId, SupportedCurrencyIds = [register.CurrencyId], TaxEnabled = true, TaxRatePercent = 10 };
        var identity = new Mock<IIdentityServerServiceWrapper>(MockBehavior.Strict);
        identity.Setup(x => x.GetTenantFinanceSettings(It.IsAny<GetTenantFinanceSettingsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new QueryResponse<TenantFinanceSettingsResponse>
            { HttpStatusCode = HttpStatusCode.OK, Response = new() { IsUsable = true, TenantId = tenant, Settings = settings } });
        var productId = Guid.NewGuid();
        var inventory = new Mock<IInventarioServiceWrapper>(MockBehavior.Strict);
        inventory.Setup(x => x.GetSellableProduct(It.IsAny<GetSellableProductRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse<SellableProductDetail>
            { HttpStatusCode = HttpStatusCode.OK, Response = new(productId, "Tax item", null, null, null, null, Guid.NewGuid(), null, true, 100, []) });
        inventory.Setup(x => x.ReserveInventory(It.IsAny<ReserveInventoryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse { HttpStatusCode = HttpStatusCode.Conflict, Message = "Stop before payment" });
        var resolver = new Mock<IPosRequestContextResolver>();
        resolver.Setup(x => x.Resolve(It.IsAny<RequestBase>(), It.IsAny<Guid?>()))
            .Returns((RequestBase r, Guid? actor) => Result<PosRequestContext>.Success(new(tenant, actor, r.Metadata, true, false)));
        var finance = new PosFinanceSettings(identity.Object);
        var features = new Mock<XFramework.Core.Services.FeatureGates.ITenantModuleFeatureService>();
        features.Setup(x => x.EnsureEnabledAsync(tenant, "inventario", "fulfillment", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var sales = new PosSalesService(db, inventory.Object, null!, resolver.Object, NullLogger<PosSalesService>.Instance, finance, new PosCheckoutSettingsService(db, resolver.Object), features.Object);
        var carts = new PosCartService(db, inventory.Object, sales, resolver.Object, NullLogger<PosCartService>.Instance, finance, new PosCheckoutSettingsService(db, resolver.Object));
        var cart = await carts.CreateAsync(new()
        { RegisterId = register.Id, CashierCredentialId = Guid.NewGuid(), DiscountAmount = 10, Lines = [new() { ProductId = productId, Quantity = 1, DiscountAmount = 20 }] }, CancellationToken.None);
        cart.IsSuccess.Should().BeTrue(cart.Message);
        cart.Data!.TaxAmount.Should().Be(7);
        cart.Data.TotalAmount.Should().Be(77);
        var request = new CheckoutPosSaleRequest
        {
            RegisterId = register.Id, CashierCredentialId = Guid.NewGuid(), DiscountAmount = 10, IdempotencyKey = Guid.NewGuid().ToString(),
            Lines = [new() { ProductId = productId, Quantity = 1, ExpectedUnitPrice = 100, DiscountAmount = 20 }],
            Payment = new() { Method = PosPaymentMethod.CashDrawer, Amount = 77, CashTenderedAmount = 77 }
        };
        var sale = await sales.CheckoutAsync(request, CancellationToken.None);
        sale.IsSuccess.Should().BeTrue(sale.Message);
        sale.Data!.TaxAmount.Should().Be(7);
        sale.Data.TotalAmount.Should().Be(77);
        db.ChangeTracker.Clear();
        (await db.Set<PosSale>().SingleAsync()).UsesAutomaticTax.Should().BeTrue();
        settings.TaxRatePercent = 25;
        var replay = await sales.CheckoutAsync(request, CancellationToken.None);
        replay.IsSuccess.Should().BeTrue(replay.Message);
        replay.Data!.TaxAmount.Should().Be(7);
        identity.Verify(x => x.GetTenantFinanceSettings(It.IsAny<GetTenantFinanceSettingsRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        var changedRate = await carts.CheckoutAsync(new()
        {
            CartId = cart.Data.Id, ExpectedConcurrencyStamp = cart.Data.ConcurrencyStamp,
            IdempotencyKey = Guid.NewGuid().ToString(), Payment = new() { Method = PosPaymentMethod.WalletTransfer, Amount = 77, CustomerCredentialId = Guid.NewGuid() }
        }, CancellationToken.None);
        changedRate.StatusCode.Should().Be(409);
        changedRate.Message.Should().Contain("Payment amount does not match");
        (await db.Set<PosSale>().CountAsync()).Should().Be(1, "a changed quote must not persist or charge a new sale");
    }

    [Test]
    public async Task PosSettings_AdminSave_CashierRead_ConcurrencyTenantAndRepairAreEnforced()
    {
        var tenant = Guid.NewGuid();
        await using var db = Context(tenant);
        db.Add(new Tenant { Id = tenant, TenantId = tenant, Name = "POS settings test", Version = 1, IsEnabled = true });
        await db.SaveChangesAsync();
        var admin = PosSettings(db, tenant, true);
        var cashier = PosSettings(db, tenant, false);
        var settings = new PosCheckoutSettings { TableTrackingEnabled = true,
            CheckoutFields = [new() { Key = "coupon", Label = "Coupon", Enabled = true, Required = true }] };
        (await cashier.UpdateAsync(new() { Settings = settings }, CancellationToken.None)).StatusCode.Should().Be(403);
        (await admin.UpdateAsync(new() { Settings = settings, Metadata = new() { RequestedTenantId = Guid.NewGuid() } },
            CancellationToken.None)).StatusCode.Should().Be(403);
        var saved = await admin.UpdateAsync(new() { Settings = settings }, CancellationToken.None);
        saved.IsSuccess.Should().BeTrue(saved.Message);
        saved.Data!.CanManage.Should().BeTrue();
        var read = await cashier.GetAsync(new(), CancellationToken.None);
        read.IsSuccess.Should().BeTrue(read.Message);
        read.Data!.CanManage.Should().BeFalse();
        read.Data.Settings.Should().BeEquivalentTo(settings);
        (await admin.UpdateAsync(new() { Settings = settings }, CancellationToken.None)).StatusCode.Should().Be(409);
        await using (var otherDb = Context(Guid.NewGuid()))
            (await otherDb.Set<RegistryConfiguration>().AnyAsync(x => x.Key == PosCheckoutSettings.RegistryKey)).Should().BeFalse();

        var row = await db.Set<RegistryConfiguration>().AsTracking().SingleAsync(x => x.Key == PosCheckoutSettings.RegistryKey);
        row.Value = "{invalid";
        await db.SaveChangesAsync();
        (await cashier.GetAsync(new(), CancellationToken.None)).StatusCode.Should().Be(409);
        var repair = await admin.GetAsync(new(), CancellationToken.None);
        repair.IsSuccess.Should().BeTrue();
        repair.Data!.IsUsable.Should().BeFalse();
        var repaired = await admin.UpdateAsync(new()
        {
            ExpectedConcurrencyStamp = repair.Data.ConcurrencyStamp, Settings = settings
        }, CancellationToken.None);
        repaired.IsSuccess.Should().BeTrue(repaired.Message);
        repaired.Data!.IsUsable.Should().BeTrue();
    }

    private static TenantPosSettingsService PosSettings(AppDbContext db, Guid tenant, bool canManage)
    {
        var actor = new TrustedActorIdentity(Guid.NewGuid(), null, tenant, Guid.NewGuid(), new HashSet<string>(),
            canManage ? new HashSet<string> { "identity.tenants:manage", "pos.sales:view" } : new HashSet<string> { "pos.sales:view" },
            "test", DateTimeOffset.UtcNow.AddHours(1));
        var accessor = new Mock<ITrustedInvocationContextAccessor>();
        accessor.SetupGet(x => x.Current).Returns(new TrustedInvocationContext(actor, null, tenant, tenant, Guid.NewGuid()));
        return new(db, accessor.Object, NullLogger<TenantPosSettingsService>.Instance);
    }

    private AppDbContext Context(Guid tenant) => new(options, new HttpContextAccessor(), new ConfigurationBuilder().Build(), new TestEffectiveTenantContextAccessor(tenant));
    private static CurrencyType Currency(Guid tenant, string code) => new()
    { Id = Guid.NewGuid(), TenantId = tenant, CurrencyIsoCode3 = code, Name = code, IsEnabled = true, SystemReferenceId = Guid.NewGuid() };
    private static TenantFinanceSettingsService Finance(AppDbContext db, Guid tenant, bool canManage = true)
    {
        var actor = new TrustedActorIdentity(Guid.NewGuid(), null, tenant, Guid.NewGuid(), new HashSet<string>(), canManage ? new HashSet<string> { "wallets:manage" } : new HashSet<string>(), "test", DateTimeOffset.UtcNow.AddHours(1));
        var accessor = new Mock<ITrustedInvocationContextAccessor>();
        accessor.SetupGet(x => x.Current).Returns(new TrustedInvocationContext(actor, null, tenant, tenant, Guid.NewGuid()));
        return new(db, accessor.Object, NullLogger<TenantFinanceSettingsService>.Instance);
    }
}
