using Microsoft.EntityFrameworkCore;
using Wallets.Domain.Shared.Contracts;
using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Enums;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;
using XFramework.TestInfrastructure;

namespace Wallets.IntegrationTests.Tests;

[TestFixture]
[NonParallelizable]
public sealed class FinancialReportTests : WalletsTestBase
{
    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public async Task Wrapper_Report_DelegatedTenant_RequiresReportingAndTenantManagement(bool canReport, bool canManageTenants)
    {
        var owner = await SeedCredential();
        var wallet = await SeedWallet(owner.Id, 123);
        await using var db = CreateDbContext();
        var currency = new CurrencyType { Id = Guid.NewGuid(), TenantId = WalletsTestFixture.TestTenantId, Name = "Delegated report", CurrencyIsoCode3 = "USD", IsEnabled = true };
        var type = new WalletType { Id = Guid.NewGuid(), TenantId = WalletsTestFixture.TestTenantId, Name = "Delegated report", Code = Guid.NewGuid().ToString("N")[..8], CurrencyTypeId = currency.Id, IsEnabled = true };
        db.Add(currency);
        db.Add(type);
        db.Attach(wallet);
        wallet.WalletTypeId = type.Id;
        await db.SaveChangesAsync();

        List<string> capabilities = [];
        if (canReport) capabilities.Add(WalletAuthorizationCapabilities.ReportingView);
        if (canManageTenants) capabilities.Add(XFrameworkActorCapabilities.IdentityTenantsManage);
        var token = TestInvocationIdentityExtensions.CreateTestActorToken(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), [], capabilities);
        using var scope = TestInvocationActorTokenScope.Push(token);
        var response = await WalletsTestFixture.ServiceWrapper.WalletFinancialReport(new()
        {
            WalletId = wallet.Id,
            From = DateTime.UtcNow.Date,
            ToExclusive = DateTime.UtcNow.Date.AddDays(1),
            Metadata = CreateMetadata()
        });

        response.IsSuccess.Should().Be(canReport && canManageTenants, response.Message);
        if (canReport && canManageTenants)
        {
            response.Response!.TenantId.Should().Be(WalletsTestFixture.TestTenantId);
            response.Response.Currencies.Should().ContainSingle().Which.Balance.Should().Be(123);
            response.Response.Currencies.Single().CurrencyId.Should().Be(currency.Id);
        }
        else
        {
            response.HttpStatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        }
    }

    [Test]
    public async Task Wrapper_Report_SeparatesCurrencies_ExcludesHoldsDeletedAndOutOfPeriodEntries()
    {
        var actor = await SeedCredential();
        var usd = await SeedWallet(actor.Id, 100);
        var eur = new Wallet { Id = Guid.NewGuid(), CredentialId = actor.Id, Balance = 200, TenantId = WalletsTestFixture.TestTenantId, Status = WalletStatus.Active };
        var from = DateTime.UtcNow.Date.AddDays(-2);
        var through = from.AddDays(3);
        await using var db = CreateDbContext();
        var currency = new CurrencyType { Id = Guid.NewGuid(), TenantId = WalletsTestFixture.TestTenantId, Name = "Euro report test", CurrencyIsoCode3 = "EUR", IsEnabled = true };
        var type = new WalletType { Id = Guid.NewGuid(), TenantId = WalletsTestFixture.TestTenantId, Name = "Euro report test", Code = Guid.NewGuid().ToString("N")[..8], CurrencyTypeId = currency.Id, IsEnabled = true };
        db.Add(currency); db.Add(type);
        eur.WalletTypeId = type.Id;
        db.Add(eur);
        var dollarCurrency = Guid.NewGuid();
        var dollarType = new WalletType { Id = Guid.NewGuid(), TenantId = WalletsTestFixture.TestTenantId, Name = "Dollar report test", Code = Guid.NewGuid().ToString("N")[..8], CurrencyTypeId = dollarCurrency, IsEnabled = true };
        db.Add(new CurrencyType { Id = dollarCurrency, TenantId = WalletsTestFixture.TestTenantId, Name = "Dollar report test", CurrencyIsoCode3 = "USD", IsEnabled = true });
        db.Add(dollarType);
        db.Attach(usd);
        usd.WalletTypeId = dollarType.Id;
        var operation = new WalletOperation { Id = Guid.NewGuid(), TenantId = WalletsTestFixture.TestTenantId, Status = WalletOperationStatus.Completed };
        db.Add(operation);
        var entries = new[] {
            Entry(operation.Id, usd.Id, dollarCurrency, 10, from.AddHours(1)),
            Entry(operation.Id, eur.Id, currency.Id, 20, from.AddHours(1)),
            Entry(operation.Id, usd.Id, null, 3, from.AddHours(2)),
            Entry(operation.Id, eur.Id, null, 7, from.AddHours(2)),
            Entry(operation.Id, eur.Id, currency.Id, 500, from.AddHours(1), WalletBalanceBucket.DebitHold),
            Entry(operation.Id, eur.Id, currency.Id, 500, through),
            Entry(operation.Id, eur.Id, currency.Id, 500, from.AddHours(1), deleted: true) };
        for (var i = 0; i < entries.Length; i++) entries[i].Sequence = i + 1;
        db.AddRange(entries);
        await db.SaveChangesAsync();
        using var scope = WalletsTestFixture.PushActor(actor.Id, [WalletAuthorizationCapabilities.ReportingView]);
        var response = await WalletsTestFixture.ServiceWrapper.WalletFinancialReport(new() { From = from, ToExclusive = through, Metadata = CreateMetadata() });
        response.IsSuccess.Should().BeTrue(response.Message);
        response.Response!.TenantId.Should().Be(WalletsTestFixture.TestTenantId);
        response.Response.Currencies.Single(x => x.CurrencyId == currency.Id).Credits.Should().Be(27);
        response.Response.Currencies.Single(x => x.CurrencyId == currency.Id).Balance.Should().Be(200);
        response.Response.Currencies.Single(x => x.CurrencyId == dollarCurrency).Credits.Should().Be(13);
        response.Response.Currencies.Should().NotContain(x => x.CurrencyId == null);
        response.Response.DailyActivity.Should().HaveCount(2);
    }

    [Test]
    public async Task Wrapper_Report_RejectsUnknownCurrencies_InsteadOfCombiningAmounts()
    {
        var actor = await SeedCredential();
        await SeedWallet(actor.Id);
        using var scope = WalletsTestFixture.PushActor(actor.Id, [WalletAuthorizationCapabilities.ReportingView]);
        var response = await WalletsTestFixture.ServiceWrapper.WalletFinancialReport(new() { From = DateTime.UtcNow.Date, ToExclusive = DateTime.UtcNow.Date.AddDays(1), Metadata = CreateMetadata() });
        response.IsSuccess.Should().BeFalse();
        response.Message.Should().Contain("currency");
    }

    [Test]
    public async Task Wrapper_Report_RejectsOtherActorsWalletAndTenantSpoofing()
    {
        var owner = await SeedCredential();
        var other = await SeedCredential();
        var wallet = await SeedWallet(owner.Id);
        using var scope = WalletsTestFixture.PushActor(other.Id, [WalletAuthorizationCapabilities.ReportingView]);
        var request = new WalletFinancialReportRequest { WalletId = wallet.Id, From = DateTime.UtcNow.Date, ToExclusive = DateTime.UtcNow.Date.AddDays(1), Metadata = CreateMetadata() };
        (await WalletsTestFixture.ServiceWrapper.WalletFinancialReport(request)).IsSuccess.Should().BeFalse();
        var metadata = CreateMetadata(); metadata.RequestedTenantId = Guid.NewGuid();
        (await WalletsTestFixture.ServiceWrapper.WalletFinancialReport(request with { WalletId = null, Metadata = metadata })).IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task Wrapper_Report_RequiresReportingPermission_RejectsInvalidRange()
    {
        var actor = await SeedCredential();
        var request = new WalletFinancialReportRequest { From = DateTime.UtcNow.Date, ToExclusive = DateTime.UtcNow.Date.AddDays(367), Metadata = CreateMetadata() };
        using (WalletsTestFixture.PushActor(actor.Id, [WalletAuthorizationCapabilities.ReportingView]))
            (await WalletsTestFixture.ServiceWrapper.WalletFinancialReport(request)).IsSuccess.Should().BeFalse();
        using (WalletsTestFixture.PushActor(actor.Id, [WalletAuthorizationCapabilities.View]))
            (await WalletsTestFixture.ServiceWrapper.WalletFinancialReport(request with { ToExclusive = request.From.AddDays(1) })).IsSuccess.Should().BeFalse();
    }

    private static WalletLedgerEntry Entry(Guid operationId, Guid walletId, Guid? currencyId, decimal amount, DateTime created, WalletBalanceBucket bucket = WalletBalanceBucket.Available, bool deleted = false) => new() {
        Id = Guid.NewGuid(), OperationId = operationId, WalletId = walletId, CurrencyId = currencyId,
        Amount = amount, CreatedAt = created, TenantId = WalletsTestFixture.TestTenantId,
        Direction = WalletLedgerDirection.Credit, BalanceBucket = bucket, IsDeleted = deleted
    };
}
