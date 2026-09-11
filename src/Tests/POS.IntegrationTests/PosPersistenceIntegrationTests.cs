using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using POS.Api.Services;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Enums;
using Testcontainers.PostgreSql;
using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Integration.Drivers;
using Inventario.Integration.Drivers;
using XFramework.Core.Patterns;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;
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
        firstPayment.Amount = 15;
        firstPayment.CashTenderedAmount = 20;
        firstPayment.ChangeAmount = 5;
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
            var persistedPayment = await db.Set<PosPayment>().SingleAsync(item => item.Id == firstPayment.Id);
            persistedPayment.CashTenderedAmount.Should().Be(20);
            persistedPayment.ChangeAmount.Should().Be(5);
        }
    }

    [Test]
    public async Task CreateReturn_CrossMethodRequest_IsRejectedBeforeInventoryOrWalletMutation()
    {
        var tenantId = Guid.NewGuid();
        var seed = CreateCompletedSale(tenantId, PosPaymentMethod.CashDrawer);
        await using var db = CreateContext(tenantId);
        db.AddRange(seed.Register, seed.Sale, seed.Line, seed.Payment);
        await db.SaveChangesAsync();

        var inventario = new Mock<IInventarioServiceWrapper>(MockBehavior.Strict);
        var wallets = new Mock<IWalletsServiceWrapper>(MockBehavior.Strict);
        var service = CreateReturnsService(db, tenantId, inventario, wallets);

        var result = await service.CreateAsync(CreateReturnRequest(
            seed,
            PosPaymentMethod.WalletTransfer), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        result.Message.Should().Be("Refund method must match the original captured payment method");
        (await db.Set<PosReturn>().CountAsync()).Should().Be(0);
        inventario.VerifyNoOtherCalls();
        wallets.VerifyNoOtherCalls();
    }

    [Test]
    public async Task CreateReturn_AfterRegisterWalletEdit_RefundsOriginalCapturedWallet()
    {
        var tenantId = Guid.NewGuid();
        var seed = CreateCompletedSale(tenantId, PosPaymentMethod.CashDrawer);
        var originalMerchantCredentialId = seed.Payment.MerchantCredentialId;
        var originalWalletId = seed.Payment.WalletId!.Value;
        var replacementMerchantCredentialId = Guid.NewGuid();
        var replacementWalletId = Guid.NewGuid();

        await using var db = CreateContext(tenantId);
        db.AddRange(seed.Register, seed.Sale, seed.Line, seed.Payment);
        await db.SaveChangesAsync();
        seed.Register.MerchantCredentialId = replacementMerchantCredentialId;
        seed.Register.CashDrawerWalletId = replacementWalletId;
        await db.SaveChangesAsync();

        var inventario = new Mock<IInventarioServiceWrapper>(MockBehavior.Strict);
        inventario.Setup(item => item.PostStockMovement(
                It.IsAny<PostStockMovementRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse { HttpStatusCode = HttpStatusCode.OK });
        var wallets = new Mock<IWalletsServiceWrapper>(MockBehavior.Strict);
        wallets.Setup(item => item.DecrementWallet(It.IsAny<DecrementWalletRequest>()))
            .ReturnsAsync(new CmdResponse { HttpStatusCode = HttpStatusCode.OK });
        var service = CreateReturnsService(db, tenantId, inventario, wallets);

        var result = await service.CreateAsync(CreateReturnRequest(
            seed,
            PosPaymentMethod.CashDrawer), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.Status.Should().Be(PosReturnStatus.Completed);
        wallets.Verify(item => item.DecrementWallet(It.Is<DecrementWalletRequest>(request =>
            request.CredentialId == originalMerchantCredentialId &&
            request.WalletId == originalWalletId &&
            request.CredentialId != replacementMerchantCredentialId &&
            request.WalletId != replacementWalletId &&
            request.WalletTypeId == seed.Payment.WalletTypeId &&
            request.CurrencyId == seed.Payment.CurrencyId &&
            request.Amount == seed.Sale.TotalAmount)), Times.Once);
        wallets.VerifyNoOtherCalls();
        inventario.VerifyAll();
    }

    private AppDbContext CreateContext(Guid tenantId) => new(
        options,
        new HttpContextAccessor(),
        new ConfigurationBuilder().Build(),
        new TestEffectiveTenantContextAccessor(tenantId));

    private static PosReturnsService CreateReturnsService(
        AppDbContext db,
        Guid tenantId,
        Mock<IInventarioServiceWrapper> inventario,
        Mock<IWalletsServiceWrapper> wallets)
    {
        var resolver = new Mock<IPosRequestContextResolver>(MockBehavior.Strict);
        resolver.Setup(item => item.Resolve(It.IsAny<RequestBase>(), It.IsAny<Guid?>()))
            .Returns((RequestBase request, Guid? _) => Result<PosRequestContext>.Success(
                new PosRequestContext(tenantId, Guid.NewGuid(), request.Metadata, true, false)));
        return new PosReturnsService(db, inventario.Object, wallets.Object, resolver.Object);
    }

    private static CreatePosReturnRequest CreateReturnRequest(
        CompletedSaleSeed seed,
        PosPaymentMethod refundMethod) => new()
    {
        SaleId = seed.Sale.Id,
        CashierCredentialId = Guid.NewGuid(),
        RefundMethod = refundMethod,
        IdempotencyKey = $"return-{Guid.NewGuid():N}",
        Lines = [new CreatePosReturnLineRequest { SaleLineId = seed.Line.Id, Quantity = seed.Line.Quantity }],
        Metadata = new RequestMetadata { RequestedTenantId = seed.Sale.TenantId }
    };

    private static CompletedSaleSeed CreateCompletedSale(Guid tenantId, PosPaymentMethod paymentMethod)
    {
        var register = CreateRegister(tenantId);
        var sale = CreateSale(tenantId, register.Id);
        sale.Status = PosSaleStatus.Completed;
        sale.PaymentMethod = paymentMethod;
        sale.SubtotalAmount = 25;
        sale.TotalAmount = 25;
        sale.CompletedAt = DateTime.UtcNow;
        sale.Register = register;
        var line = new PosSaleLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SaleId = sale.Id,
            LineNumber = 1,
            ProductId = Guid.NewGuid(),
            ProductName = "Refund regression item",
            Quantity = 1,
            UnitPrice = 25,
            ExpectedUnitPrice = 25,
            LineTotal = 25,
            WarehouseId = sale.WarehouseId,
            LocationId = sale.LocationId,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid(),
            IsEnabled = true,
            Sale = sale
        };
        var payment = CreatePayment(tenantId, sale.Id, Guid.NewGuid());
        payment.Method = paymentMethod;
        payment.Status = PosPaymentStatus.Captured;
        payment.Amount = sale.TotalAmount;
        payment.CurrencyId = sale.CurrencyId;
        payment.WalletTypeId = sale.WalletTypeId;
        payment.WalletId = paymentMethod == PosPaymentMethod.CashDrawer ? Guid.NewGuid() : null;
        payment.CustomerCredentialId = paymentMethod == PosPaymentMethod.WalletTransfer ? Guid.NewGuid() : null;
        payment.CapturedAt = DateTime.UtcNow;
        payment.Sale = sale;
        sale.Lines = [line];
        sale.Payments = [payment];
        return new CompletedSaleSeed(register, sale, line, payment);
    }

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

    private sealed record CompletedSaleSeed(
        PosRegister Register,
        PosSale Sale,
        PosSaleLine Line,
        PosPayment Payment);
}
