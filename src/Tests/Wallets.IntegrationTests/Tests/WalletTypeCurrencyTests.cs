using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wallets.Domain.Shared.Contracts;
using XFramework.Domain.Shared.DataContext;
using XFramework.TestInfrastructure;
using SharedContracts = XFramework.Domain.Shared.Contracts;

namespace Wallets.IntegrationTests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category("ReferenceData")]
public sealed class WalletTypeCurrencyTests : WalletsTestBase
{
    [Test]
    public async Task RemoteMutation_CurrencyOwnershipAndStatus_AreValidated(
        [Values(false, true)] bool update,
        [Values("tenant", "global", "foreign", "disabled", "deleted", "missing", "empty")] string currencyKind)
    {
        using var actor = UseCatalogAdmin();
        var currencyId = await SeedCurrency(currencyKind);
        var type = NewWalletType();
        DataContextResult result;
        if (update)
        {
            await using var db = CreateDbContext();
            db.Add(type);
            await db.SaveChangesAsync();
            result = await ChangeCurrency(type.Id, currencyId);
        }
        else
        {
            type.CurrencyTypeId = currencyId;
            var remote = WalletsTestFixture.DataContext;
            remote.Add(type);
            result = await remote.SaveChangesAsync();
        }

        var allowed = currencyKind is "tenant" or "global";
        result.IsSuccess.Should().Be(allowed, result.Message);
        await using var verify = CreateDbContext();
        var persisted = await verify.Set<WalletType>().IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == type.Id);
        if (allowed)
            persisted!.CurrencyTypeId.Should().Be(currencyId);
        else
        {
            result.StatusCode.Should().Be(400);
            result.Message.Should().Contain("Currency must be active and available to this tenant.");
            if (update) persisted!.CurrencyTypeId.Should().BeNull();
            else persisted.Should().BeNull();
        }
    }

    [Test]
    public async Task RemoteUpdate_UsedType_RejectsChangingOrClearingEstablishedCurrency(
        [Values("wallet", "deleted-wallet", "ledger", "deleted-ledger", "deposit")] string usage,
        [Values(false, true)] bool clear)
    {
        using var actor = UseCatalogAdmin();
        var originalCurrency = await SeedCurrency("tenant");
        var type = await SeedType(originalCurrency, usage);
        var result = await ChangeCurrency(type.Id, clear ? null : await SeedCurrency("tenant"));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Contain("An established currency cannot be changed or cleared");
        await using var db = CreateDbContext();
        (await db.Set<WalletType>().SingleAsync(x => x.Id == type.Id)).CurrencyTypeId.Should().Be(originalCurrency);
    }

    [Test]
    public async Task RemoteUpdate_UsedTypeWithoutCurrency_AllowsInitialAssignmentWithoutConversion(
        [Values("tenant", "global")] string currencyKind)
    {
        using var actor = UseCatalogAdmin();
        var type = await SeedType(null, "wallet");
        await using (var seed = CreateDbContext())
        {
            var walletId = await seed.Set<Wallet>().Where(x => x.WalletTypeId == type.Id).Select(x => x.Id).SingleAsync();
            var operation = new WalletOperation { Id = Guid.NewGuid(), TenantId = type.TenantId };
            seed.Add(operation);
            seed.Add(new WalletLedgerEntry
            {
                Id = Guid.NewGuid(), TenantId = type.TenantId, WalletTypeId = type.Id,
                WalletId = walletId, OperationId = operation.Id, Amount = 125m, RunningBalance = 125m
            });
            await seed.SaveChangesAsync();
        }
        var currencyId = await SeedCurrency(currencyKind);
        var result = await ChangeCurrency(type.Id, currencyId);

        result.IsSuccess.Should().BeTrue(result.Message);
        await using var db = CreateDbContext();
        (await db.Set<WalletType>().SingleAsync(x => x.Id == type.Id)).CurrencyTypeId.Should().Be(currencyId);
        var wallet = await db.Set<Wallet>().SingleAsync(x => x.WalletTypeId == type.Id);
        wallet.Balance.Should().Be(125m);
        wallet.TransferableBalance.Should().Be(125m);
        var entry = await db.Set<WalletLedgerEntry>().SingleAsync(x => x.WalletTypeId == type.Id);
        entry.Amount.Should().Be(125m);
        entry.RunningBalance.Should().Be(125m);
        entry.CurrencyId.Should().BeNull();
        (await db.Set<WalletOperation>().CountAsync(x => x.Id == entry.OperationId)).Should().Be(1);
    }

    [Test]
    public async Task RemoteUpdate_UnusedType_AllowsChangingOrClearingCurrency([Values(false, true)] bool clear)
    {
        using var actor = UseCatalogAdmin();
        var type = await SeedType(await SeedCurrency("tenant"));
        Guid? currencyId = clear ? null : await SeedCurrency("global");
        var result = await ChangeCurrency(type.Id, currencyId);

        result.IsSuccess.Should().BeTrue(result.Message);
        await using var db = CreateDbContext();
        (await db.Set<WalletType>().SingleAsync(x => x.Id == type.Id)).CurrencyTypeId.Should().Be(currencyId);
    }

    [Test]
    public async Task RemoteUpdate_UsedTypeWithUnchangedCurrency_AllowsOtherEdits()
    {
        using var actor = UseCatalogAdmin();
        var currencyId = await SeedCurrency("tenant");
        var type = await SeedType(currencyId, "wallet");
        var remote = WalletsTestFixture.DataContext;
        var current = (await remote.Query<WalletType>().Where(x => x.Id == type.Id).FirstOrDefaultAsync())!;
        current.Name = "Renamed";
        remote.Update(current);
        var result = await remote.SaveChangesAsync();

        result.IsSuccess.Should().BeTrue(result.Message);
        await using var db = CreateDbContext();
        var persisted = await db.Set<WalletType>().SingleAsync(x => x.Id == type.Id);
        persisted.Name.Should().Be("Renamed");
        persisted.CurrencyTypeId.Should().Be(currencyId);
    }

    private static IDisposable UseCatalogAdmin() => WalletsTestFixture.PushActor(Guid.NewGuid(),
        new[] { WalletAuthorizationCapabilities.View, WalletAuthorizationCapabilities.Create, WalletAuthorizationCapabilities.Update });

    private static WalletType NewWalletType() => new()
    {
        Id = Guid.NewGuid(), TenantId = WalletsTestFixture.TestTenantId,
        Name = "Currency test", Code = Guid.NewGuid().ToString("N")[..8],
        IsEnabled = true, SystemReferenceId = Guid.NewGuid(), ConcurrencyStamp = Guid.NewGuid()
    };

    private async Task<Guid> SeedCurrency(string kind)
    {
        if (kind == "missing") return Guid.NewGuid();
        if (kind == "empty") return Guid.Empty;
        var tenantId = kind switch
        {
            "foreign" => Guid.NewGuid(),
            _ => WalletsTestFixture.TestTenantId
        };
        await using var db = CreateDbContext(tenantId);
        var currency = new CurrencyType
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Name = "Currency test", CurrencyIsoCode3 = "TST",
            IsEnabled = kind != "disabled", IsDeleted = kind == "deleted", SystemReferenceId = Guid.NewGuid()
        };
        db.Add(currency);
        await db.SaveChangesAsync();
        // Global catalog rows are system-seeded, not writable via the tenant save hook.
        if (kind == "global")
            await db.Set<CurrencyType>().Where(x => x.Id == currency.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.TenantId, Guid.Empty));
        return currency.Id;
    }

    private async Task<WalletType> SeedType(Guid? currencyId, string? usage = null)
    {
        var type = NewWalletType();
        type.CurrencyTypeId = currencyId;
        await using var db = CreateDbContext();
        db.Add(type);
        if (usage is "wallet" or "deleted-wallet")
        {
            var credential = await SeedCredential();
            db.Add(new Wallet
            {
                Id = Guid.NewGuid(), TenantId = type.TenantId, WalletTypeId = type.Id,
                CredentialId = credential.Id, Balance = 125m, TransferableBalance = 125m,
                IsEnabled = true, IsDeleted = usage == "deleted-wallet"
            });
        }
        else if (usage is "ledger" or "deleted-ledger")
        {
            var operation = new WalletOperation { Id = Guid.NewGuid(), TenantId = type.TenantId };
            db.Add(operation);
            db.Add(new WalletLedgerEntry
            {
                Id = Guid.NewGuid(), TenantId = type.TenantId, WalletTypeId = type.Id,
                OperationId = operation.Id, Amount = 125m, CurrencyId = currencyId,
                IsDeleted = usage == "deleted-ledger"
            });
        }
        else if (usage == "deposit")
        {
            var credential = await SeedCredential();
            var category = new SharedContracts.PaymentGatewayCategory
            {
                Id = Guid.NewGuid(), TenantId = type.TenantId, Name = "Currency test"
            };
            var gateway = new SharedContracts.PaymentGateway
            {
                Id = Guid.NewGuid(), TenantId = type.TenantId, Name = "Currency test",
                Description = "Currency validation fixture", GatewayCategoryId = category.Id
            };
            db.AddRange(category, gateway);
            db.Add(new DepositRequest
            {
                Id = Guid.NewGuid(), TenantId = type.TenantId, WalletTypeId = type.Id,
                CredentialId = credential.Id, GatewayId = gateway.Id, Amount = 125m
            });
        }
        await db.SaveChangesAsync();
        return type;
    }

    private static async Task<DataContextResult> ChangeCurrency(Guid walletTypeId, Guid? currencyId)
    {
        var remote = WalletsTestFixture.DataContext;
        var current = (await remote.Query<WalletType>().Where(x => x.Id == walletTypeId).FirstOrDefaultAsync())!;
        current.CurrencyTypeId = currencyId;
        remote.Update(current);
        return await remote.SaveChangesAsync();
    }
}
