using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Enums;
using Testcontainers.PostgreSql;
using XFramework.Domain.Contexts;
using XFramework.TestInfrastructure;

namespace POS.IntegrationTests;

[TestFixture]
[Category(TestCategories.POS)]
public sealed class PosPersistenceIntegrationTests
{
    private PostgreSqlContainer? postgres;
    private DbContextOptions<AppDbContext> options = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        try
        {
            postgres = new PostgreSqlBuilder()
                .WithDatabase("XFramework_POS_Test")
                .WithUsername("test_user")
                .WithPassword("test_password")
                .Build();
            await postgres.StartAsync();
        }
        catch (ArgumentException exception) when (exception.Message.Contains("Docker", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore("POS PostgreSQL integration tests require a Testcontainers-compatible Docker endpoint.");
        }

        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(PosRegister).TypeHandle);
        options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres!.GetConnectionString())
            .ConfigureWarnings(warnings => warnings.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        await using var db = CreateContext(Guid.NewGuid());
        await db.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (postgres is not null)
            await postgres.DisposeAsync();
    }

    [Test]
    public async Task PosMigration_EnforcesSingleActivePaymentAndPersistsCartRequestHash()
    {
        var tenantId = Guid.NewGuid();
        var register = CreateRegister(tenantId);
        var sale = CreateSale(tenantId, register.Id);
        var firstPayment = CreatePayment(tenantId, sale.Id, Guid.NewGuid());
        const string requestHash = "6E7F31D96835BBAC2E3514F65B6849D1082B92C89A3BCB9E7B27E5C85A67036A";
        var cart = CreateCart(tenantId, register.Id, requestHash);

        await using (var db = CreateContext(tenantId))
        {
            db.AddRange(register, sale, firstPayment, cart);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext(tenantId))
        {
            db.Add(CreatePayment(tenantId, sale.Id, Guid.NewGuid()));
            var save = () => db.SaveChangesAsync();
            await save.Should().ThrowAsync<DbUpdateException>();
        }

        await using (var db = CreateContext(tenantId))
        {
            var persistedHash = await db.Set<PosCart>()
                .Where(item => item.Id == cart.Id)
                .Select(item => item.RequestHash)
                .SingleAsync();
            persistedHash.Should().Be(requestHash);
        }
    }

    private AppDbContext CreateContext(Guid tenantId) => new(
        options,
        new HttpContextAccessor(),
        new ConfigurationBuilder().Build(),
        new TestEffectiveTenantContextAccessor(tenantId));

    private static PosRegister CreateRegister(Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Name = "Integration register",
        MerchantCredentialId = Guid.NewGuid(),
        CashDrawerWalletId = Guid.NewGuid(),
        WalletTypeId = Guid.NewGuid(),
        CurrencyId = Guid.NewGuid(),
        DefaultWarehouseId = Guid.NewGuid(),
        DefaultLocationId = Guid.NewGuid(),
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid()
    };

    private static PosSale CreateSale(Guid tenantId, Guid registerId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        SaleNumber = $"TEST-{Guid.NewGuid():N}"[..30],
        RegisterId = registerId,
        CashierCredentialId = Guid.NewGuid(),
        WarehouseId = Guid.NewGuid(),
        LocationId = Guid.NewGuid(),
        CurrencyId = Guid.NewGuid(),
        WalletTypeId = Guid.NewGuid(),
        IdempotencyKey = Guid.NewGuid().ToString("N"),
        Status = PosSaleStatus.PaymentPending,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid(),
        IsEnabled = true
    };

    private static PosPayment CreatePayment(Guid tenantId, Guid saleId, Guid id) => new()
    {
        Id = id,
        TenantId = tenantId,
        SaleId = saleId,
        Method = PosPaymentMethod.CashDrawer,
        Status = PosPaymentStatus.Pending,
        CurrencyId = Guid.NewGuid(),
        WalletTypeId = Guid.NewGuid(),
        MerchantCredentialId = Guid.NewGuid(),
        ReferenceNumber = $"PAY-{id:N}",
        IdempotencyKey = $"PAY-{id:N}",
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid(),
        IsEnabled = true
    };

    private static PosCart CreateCart(Guid tenantId, Guid registerId, string requestHash) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        CartNumber = $"CART-{Guid.NewGuid():N}"[..32],
        RegisterId = registerId,
        CashierCredentialId = Guid.NewGuid(),
        WarehouseId = Guid.NewGuid(),
        LocationId = Guid.NewGuid(),
        CurrencyId = Guid.NewGuid(),
        WalletTypeId = Guid.NewGuid(),
        IdempotencyKey = Guid.NewGuid().ToString("N"),
        RequestHash = requestHash,
        Status = PosCartStatus.Open,
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid(),
        IsEnabled = true
    };
}
