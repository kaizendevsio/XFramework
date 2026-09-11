using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wallets.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.TestInfrastructure;

namespace Wallets.IntegrationTests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category("ReferenceData")]
public sealed class ReferenceDataTests : WalletsTestBase
{
    private static IDisposable UseCatalogAdmin() =>
        WalletsTestFixture.PushActor(Guid.NewGuid(),
            new[] { WalletAuthorizationCapabilities.View, WalletAuthorizationCapabilities.Create,
                WalletAuthorizationCapabilities.Update, WalletAuthorizationCapabilities.Delete });
    [Test]
    public Task Currency_CreateUpdateDelete() =>
        AssertLifecycle(new CurrencyType { Name = "Test currency", CurrencyIsoCode3 = "TST" },
            currency => currency.Name = "Updated", currency => currency.Name.Should().Be("Updated"));

    [Test]
    public Task WalletType_CreateUpdateDelete() =>
        AssertLifecycle(new WalletType { Name = "Test wallet", Code = "REFTEST", Type = 1 },
            type => type.Name = "Updated", type => type.Name.Should().Be("Updated"));

    [Test]
    public async Task ExchangeRate_CreateUpdateDelete()
    {
        var (source, target) = await SeedCurrencies();
        await AssertLifecycle(new ExchangeRate { SourceCurrencyTypeId = source.Id, TargetCurrencyTypeId = target.Id, Value = 1.5m },
            rate => rate.Value = 2m, rate => rate.Value.Should().Be(2m));
    }

    [Test]
    public async Task Currency_WithoutCreateCapability_IsForbidden()
    {
        using var actor = WalletsTestFixture.PushActor(Guid.NewGuid(), new[] { WalletAuthorizationCapabilities.View });
        var context = WalletsTestFixture.DataContext;
        var currency = NewCurrency();
        context.Add(currency);
        var result = await context.SaveChangesAsync();
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.Message.Should().NotContain("change request failed with status");
        await using var db = CreateDbContext();
        (await db.Set<CurrencyType>().IgnoreQueryFilters().AnyAsync(x => x.Id == currency.Id)).Should().BeFalse();
    }

    [Test]
    public async Task ExchangeRate_CrossTenantCurrency_IsRejected()
    {
        using var actor = UseCatalogAdmin();
        var (source, target) = await SeedCurrencies();
        await using var db = CreateDbContext();
        target = NewCurrency();
        target.TenantId = Guid.NewGuid();
        await using (var otherDb = CreateDbContext(target.TenantId))
        {
            otherDb.Add(target);
            await otherDb.SaveChangesAsync();
        }

        var rate = new ExchangeRate
        {
            Id = Guid.NewGuid(), TenantId = WalletsTestFixture.TestTenantId,
            SourceCurrencyTypeId = source.Id, TargetCurrencyTypeId = target.Id, Value = 1m,
            IsEnabled = true, CreatedAt = DateTime.UtcNow
        };
        var context = WalletsTestFixture.DataContext;
        context.Add(rate);
        var result = await context.SaveChangesAsync();
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Target currency");
        (await db.Set<ExchangeRate>().IgnoreQueryFilters().AnyAsync(x => x.Id == rate.Id)).Should().BeFalse();
    }

    [Test]
    public async Task Currency_InvalidNameAndTenant_AreRejected()
    {
        using var actor = UseCatalogAdmin();
        var invalidName = NewCurrency();
        invalidName.Name = "";
        var wrongTenant = NewCurrency();
        wrongTenant.TenantId = Guid.NewGuid();
        foreach (var currency in new[] { invalidName, wrongTenant })
        {
            var context = WalletsTestFixture.DataContext;
            context.Add(currency);
            var result = await context.SaveChangesAsync();
            result.IsSuccess.Should().BeFalse();
            await using var db = CreateDbContext();
            (await db.Set<CurrencyType>().IgnoreQueryFilters().AnyAsync(x => x.Id == currency.Id)).Should().BeFalse();
        }
    }

    private async Task AssertLifecycle<T>(T entity, Action<T> mutate, Action<T> assertUpdated) where T : BaseModel
    {
        using var actor = UseCatalogAdmin();
        entity.Id = Guid.NewGuid();
        entity.TenantId = WalletsTestFixture.TestTenantId;
        entity.IsEnabled = true;
        entity.CreatedAt = DateTime.UtcNow;
        entity.ConcurrencyStamp = Guid.NewGuid();
        if (entity is IHasSystemReferenceId reference) reference.SystemReferenceId = Guid.NewGuid();
        var createContext = WalletsTestFixture.DataContext;
        createContext.Add(entity);
        var create = await createContext.SaveChangesAsync();
        create.IsSuccess.Should().BeTrue(create.Message);

        var updateContext = WalletsTestFixture.DataContext;
        var current = await updateContext.Query<T>().Where(x => x.Id == entity.Id).FirstOrDefaultAsync();
        current.Should().NotBeNull();
        mutate(current!);
        updateContext.Update(current!);
        var update = await updateContext.SaveChangesAsync();
        update.IsSuccess.Should().BeTrue(update.Message);
        await using (var db = CreateDbContext())
            assertUpdated(await db.Set<T>().AsNoTracking().SingleAsync(x => x.Id == entity.Id));

        var deleteContext = WalletsTestFixture.DataContext;
        deleteContext.Remove(current!);
        var delete = await deleteContext.SaveChangesAsync();
        delete.IsSuccess.Should().BeTrue(delete.Message);
        await using var deletedDb = CreateDbContext();
        (await deletedDb.Set<T>().IgnoreQueryFilters().SingleAsync(x => x.Id == entity.Id)).IsDeleted.Should().BeTrue();
    }

    private static CurrencyType NewCurrency() => new()
    {
        Id = Guid.NewGuid(), TenantId = WalletsTestFixture.TestTenantId, Name = "Test",
        CurrencyIsoCode3 = "TST", IsEnabled = true, CreatedAt = DateTime.UtcNow, SystemReferenceId = Guid.NewGuid()
    };

    private async Task<(CurrencyType, CurrencyType)> SeedCurrencies()
    {
        var source = NewCurrency();
        var target = NewCurrency();
        await using var db = CreateDbContext();
        db.AddRange(source, target);
        await db.SaveChangesAsync();
        return (source, target);
    }
}
