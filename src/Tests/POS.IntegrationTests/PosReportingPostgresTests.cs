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
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Contexts;
using XFramework.Integration.Security;
using XFramework.TestInfrastructure;
using CurrencyType = Wallets.Domain.Shared.Contracts.CurrencyType;

namespace POS.IntegrationTests;

[TestFixture, NonParallelizable]
[Category("Kind:Integration"), Category("Module:POS"), Category("Area:Reports")]
public sealed class PosReportingPostgresTests
{
    private PostgreSqlContainer? postgres;
    private DbContextOptions<AppDbContext> options = null!;
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [OneTimeSetUp]
    public async Task StartDatabase()
    {
        try
        {
            postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
            await postgres.StartAsync();
        }
        catch (ArgumentException ex) when (ex.Message.Contains("Docker", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore("POS reporting PostgreSQL tests require a Testcontainers-compatible Docker endpoint.");
        }
        catch (DotNet.Testcontainers.Builders.DockerUnavailableException)
        {
            Assert.Ignore("POS reporting PostgreSQL tests require a reachable Docker endpoint.");
        }
        options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(postgres!.GetConnectionString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)).Options;
        await using var db = Context(Guid.NewGuid());
        await db.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public async Task StopDatabase() { if (postgres is not null) await postgres.DisposeAsync(); }

    [Test]
    public async Task Report_AggregatesInPostgres_SeparatesCurrencies_ExcludesDeletedForeignAndOutOfRangeSales()
    {
        var tenant = Guid.NewGuid(); var other = Guid.NewGuid();
        var usd = Guid.NewGuid(); var sgd = Guid.NewGuid();
        var register = Register(tenant, usd); var foreignRegister = Register(other, usd);
        var completed = Enumerable.Range(0, 31).Select(i => Sale(register, usd, Start.AddHours(i % 3).AddMinutes(i), 10)).ToList();
        var secondCurrency = Sale(register, sgd, Start.AddDays(1), 100);
        secondCurrency.PaymentMethod = PosPaymentMethod.WalletTransfer;
        var pending = Sale(register, usd, Start.AddHours(3), 900); pending.Status = PosSaleStatus.PaymentFailed;
        var deleted = Sale(register, usd, Start.AddHours(4), 500); deleted.IsDeleted = true;
        await using (var foreignDb = Context(other))
        {
            foreignDb.AddRange(foreignRegister, Sale(foreignRegister, usd, Start, 1000));
            await foreignDb.SaveChangesAsync();
        }
        await using var db = Context(tenant);
        db.Add(register);
        db.AddRange(completed);
        db.AddRange(secondCurrency, pending, deleted,
            Sale(register, usd, Start.AddTicks(-1), 1000),
            Sale(register, usd, Start.AddDays(2), 1000));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = Service(db, tenant);
        var request = Request(tenant);
        var first = await service.GetSalesReportAsync(request, CancellationToken.None);
        first.IsSuccess.Should().BeTrue(first.Message);
        var report = first.Data!;
        report.TotalCount.Should().Be(33);
        report.Rows.Should().HaveCount(25);
        report.Currencies.Should().HaveCount(2);
        report.Currencies.Single(x => x.CurrencyId == usd).Total.Should().Be(310);
        report.Currencies.Single(x => x.CurrencyId == usd).CompletedCount.Should().Be(31);
        report.Currencies.Single(x => x.CurrencyId == sgd).WalletTotal.Should().Be(100);
        report.Statuses.Single(x => x.Status == PosSaleStatus.PaymentFailed).Count.Should().Be(1);
        report.Timeline.Should().HaveCount(2);
        report.Timeline.Single(x => x.CurrencyId == usd).Total.Should().Be(310);
        report.Rows.Should().OnlyContain(x => x.RegisterName == register.Name);
        db.ChangeTracker.Entries().Should().BeEmpty();

        var second = (await service.GetSalesReportAsync(request with { Page = 2 }, CancellationToken.None)).Data!;
        second.Rows.Should().HaveCount(8);
        second.Rows.Select(x => x.Id).Intersect(report.Rows.Select(x => x.Id)).Should().BeEmpty();
        second.Currencies.Should().BeEquivalentTo(report.Currencies);
        var hourly = (await service.GetSalesReportAsync(request with { ToUtcExclusive = Start.AddDays(1) }, CancellationToken.None)).Data!;
        hourly.Timeline.Should().HaveCount(3);
        hourly.Timeline.Select(x => x.DateUtc.Hour).Should().Equal(0, 1, 2);
        hourly.Timeline.Sum(x => x.Total).Should().Be(310);
        var last = (await service.GetSalesReportAsync(request with { Page = 3 }, CancellationToken.None)).Data!;
        last.Rows.Should().BeEmpty(); last.TotalCount.Should().Be(33);

        var filtered = (await service.GetSalesReportAsync(request with { CurrencyId = sgd, Status = PosSaleStatus.Completed }, CancellationToken.None)).Data!;
        filtered.TotalCount.Should().Be(1);
        filtered.Rows.Should().ContainSingle(x => x.Id == secondCurrency.Id);
        filtered.Currencies.Should().ContainSingle(x => x.CurrencyId == sgd && x.Total == 100);
        var foreign = (await service.GetSalesReportAsync(request with { RegisterId = foreignRegister.Id }, CancellationToken.None)).Data!;
        foreign.TotalCount.Should().Be(0); foreign.Currencies.Should().BeEmpty(); foreign.Timeline.Should().BeEmpty();
    }

    [Test]
    public async Task Report_MoreThanCurrencyLimit_FailsExplicitlyRatherThanReturningPartialTotals()
    {
        var tenant = Guid.NewGuid(); var register = Register(tenant, Guid.NewGuid());
        var sales = Enumerable.Range(0, PosReportingService.MaxCurrencies + 1)
            .Select(_ => Sale(register, Guid.NewGuid(), Start, 10)).ToList();
        await using var db = Context(tenant);
        db.Add(register); db.AddRange(sales); await db.SaveChangesAsync();
        var service = Service(db, tenant);
        var result = await service.GetSalesReportAsync(Request(tenant), CancellationToken.None);
        result.StatusCode.Should().Be(400); result.Data.Should().BeNull();
        var filtered = await service.GetSalesReportAsync(Request(tenant) with { CurrencyId = sales[0].CurrencyId }, CancellationToken.None);
        filtered.Data!.Currencies.Should().ContainSingle(x => x.Total == 10);
    }

    private AppDbContext Context(Guid tenant) => new(options, new HttpContextAccessor(), new ConfigurationBuilder().Build(), new TestEffectiveTenantContextAccessor(tenant));

    [Test]
    public async Task Report_CurrencyLabels_AllowTenantAndGlobalRowsWithoutWallets_ExcludeForeignDeletedAndUnreferencedRows()
    {
        var tenant = Guid.NewGuid(); var other = Guid.NewGuid();
        CurrencyType Currency(Guid owner, string code) => new()
        {
            Id = Guid.NewGuid(), TenantId = owner, CurrencyIsoCode3 = code, Name = code,
            CreatedAt = Start, IsEnabled = true, SystemReferenceId = Guid.NewGuid()
        };
        var usd = Currency(tenant, "USD"); var eur = Currency(tenant, "EUR");
        var foreign = Currency(other, "PRV"); var deleted = Currency(tenant, "OLD"); deleted.IsDeleted = true;
        var unreferenced = Currency(tenant, "JPY");
        var register = Register(tenant, usd.Id);
        await using (var foreignDb = Context(other))
        {
            foreignDb.Add(foreign); await foreignDb.SaveChangesAsync();
        }
        await using var db = Context(tenant);
        db.AddRange(usd, eur, deleted, unreferenced, register);
        db.AddRange(new[] { usd.Id, eur.Id, foreign.Id, deleted.Id, Guid.NewGuid() }
            .Select(currency => Sale(register, currency, Start, 10)));
        await db.SaveChangesAsync();
        await db.Set<CurrencyType>().Where(x => x.Id == usd.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.TenantId, Guid.Empty));
        db.ChangeTracker.Clear();

        var result = await Service(db, tenant).GetSalesReportAsync(Request(tenant), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.Currencies.Should().HaveCount(5);
        result.Data.CurrencyLabels.Should().HaveCount(2);
        result.Data.CurrencyLabels.Should().Contain(x => x.CurrencyId == usd.Id && x.Label == "USD");
        result.Data.CurrencyLabels.Should().Contain(x => x.CurrencyId == eur.Id && x.Label == "EUR");
        db.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Test]
    public async Task Report_RefundDateRecognizesOlderSales_SeparatesCurrenciesAndPagesRefunds()
    {
        var tenant = Guid.NewGuid(); var currency = Guid.NewGuid(); var secondCurrency = Guid.NewGuid();
        var register = Register(tenant, currency);
        var oldSale = Sale(register, currency, Start.AddMonths(-1), 1000);
        var otherSale = Sale(register, secondCurrency, Start.AddMonths(-1), 100);
        PosReturn Refund(PosSale sale, decimal amount, DateTime completed, PosReturnStatus status = PosReturnStatus.Completed) => new()
        {
            Id = Guid.NewGuid(), TenantId = tenant, RegisterId = register.Id, SaleId = sale.Id,
            ReturnNumber = Guid.NewGuid().ToString("N"), CashierCredentialId = Guid.NewGuid(),
            CurrencyId = sale.CurrencyId, WalletTypeId = register.WalletTypeId,
            Status = status, RefundMethod = PosPaymentMethod.CashDrawer,
            TotalRefundAmount = amount, SubtotalAmount = amount, CreatedAt = Start.AddDays(-2),
            CompletedAt = completed, ConcurrencyStamp = Guid.NewGuid(), IsEnabled = true
        };
        await using var db = Context(tenant);
        db.AddRange(register, oldSale, otherSale);
        db.AddRange(Enumerable.Range(0, 26).Select(i => Refund(oldSale, 2, Start.AddMinutes(i))));
        db.AddRange(Refund(otherSale, 7, Start.AddHours(2)), Refund(oldSale, 900, Start.AddDays(2)),
            Refund(oldSale, 800, Start.AddDays(1), PosReturnStatus.RefundFailed));
        await db.SaveChangesAsync();
        var service = Service(db, tenant);
        var result = await service.GetSalesReportAsync(Request(tenant), CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Message);
        var report = result.Data!;
        report.TotalCount.Should().Be(0);
        report.RefundCount.Should().Be(27);
        report.RefundRows.Should().HaveCount(25);
        report.Currencies.Single(x => x.CurrencyId == currency).NetTotal.Should().Be(-52);
        report.Currencies.Single(x => x.CurrencyId == secondCurrency).NetTotal.Should().Be(-7);
        report.Timeline.Single(x => x.CurrencyId == currency).RefundTotal.Should().Be(52);
        var next = (await service.GetSalesReportAsync(Request(tenant) with { RefundPage = 2 }, CancellationToken.None)).Data!;
        next.RefundRows.Should().HaveCount(2);
        next.RefundRows.Select(x => x.Id).Intersect(report.RefundRows.Select(x => x.Id)).Should().BeEmpty();
        var hour = (await service.GetSalesReportAsync(Request(tenant) with { ToUtcExclusive = Start.AddDays(1), HourUtc = 2 }, CancellationToken.None)).Data!;
        hour.RefundCount.Should().Be(1);
        hour.Currencies.Should().ContainSingle(x => x.CurrencyId == secondCurrency && x.NetTotal == -7);
        var wallet = (await service.GetSalesReportAsync(Request(tenant) with { PaymentMethod = PosPaymentMethod.WalletTransfer }, CancellationToken.None)).Data!;
        wallet.RefundCount.Should().Be(0);
    }
    private static PosReportingService Service(AppDbContext db, Guid tenant)
    {
        var actor = new TrustedActorIdentity(Guid.NewGuid(), Guid.NewGuid(), tenant, Guid.NewGuid(), new HashSet<string>(),
            new HashSet<string> { PosAuthorizationCapabilities.ReportingView }, "tests", DateTimeOffset.UtcNow.AddHours(1));
        var accessor = new Accessor(new(actor, null, tenant, null, Guid.NewGuid()));
        var features = new Mock<ITenantModuleFeatureService>(MockBehavior.Strict);
        features.Setup(x => x.EnsureEnabledAsync(tenant, "pos", "reporting", It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        features.Setup(x => x.EnsureEnabledAsync(tenant, "wallets", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("Wallets is disabled.", 403));
        return new(db, new PosRequestContextResolver(accessor), accessor, features.Object, NullLogger<PosReportingService>.Instance);
    }
    private static GetPosSalesReportRequest Request(Guid tenant) => new()
    {
        Metadata = new() { RequestedTenantId = tenant }, FromUtc = Start, ToUtcExclusive = Start.AddDays(2), PageSize = 25
    };
    private static PosRegister Register(Guid tenant, Guid currency) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenant, Name = "Report register", CurrencyId = currency,
        MerchantCredentialId = Guid.NewGuid(), CashDrawerWalletId = Guid.NewGuid(), WalletTypeId = Guid.NewGuid(),
        DefaultWarehouseId = Guid.NewGuid(), DefaultLocationId = Guid.NewGuid(), IsEnabled = true,
        CreatedAt = Start, ConcurrencyStamp = Guid.NewGuid()
    };
    private static PosSale Sale(PosRegister register, Guid currency, DateTime created, decimal total) => new()
    {
        Id = Guid.NewGuid(), TenantId = register.TenantId, RegisterId = register.Id, SaleNumber = Guid.NewGuid().ToString("N"),
        CashierCredentialId = Guid.NewGuid(), WarehouseId = Guid.NewGuid(), LocationId = Guid.NewGuid(),
        CurrencyId = currency, WalletTypeId = register.WalletTypeId, Status = PosSaleStatus.Completed,
        PaymentMethod = PosPaymentMethod.CashDrawer, SubtotalAmount = total, TotalAmount = total,
        CreatedAt = created, CompletedAt = created, IsEnabled = true, ConcurrencyStamp = Guid.NewGuid()
    };
    private sealed class Accessor(TrustedInvocationContext context) : ITrustedInvocationContextAccessor
    { public TrustedInvocationContext? Current => context; }
}
