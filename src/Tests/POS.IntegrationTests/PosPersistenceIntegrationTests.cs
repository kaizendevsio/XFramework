using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using IdentityServer.Integration.Drivers;
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
using XFramework.Domain.Shared.Enums;
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

    [Test]
    public async Task RetryReturn_LostRefundReply_ReusesOriginalAccountAndKeyWithoutRestockingAgain()
    {
        var tenantId = Guid.NewGuid();
        var seed = CreateCompletedSale(tenantId, PosPaymentMethod.WalletTransfer);
        await using var db = CreateContext(tenantId);
        db.AddRange(seed.Register, seed.Sale, seed.Line, seed.Payment);
        await db.SaveChangesAsync();
        var inventory = new Mock<IInventarioServiceWrapper>(MockBehavior.Strict);
        inventory.Setup(item => item.PostStockMovement(It.IsAny<PostStockMovementRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse { HttpStatusCode = HttpStatusCode.OK });
        var refundRequests = new List<TransferWalletRequest>();
        var wallets = new Mock<IWalletsServiceWrapper>(MockBehavior.Strict);
        wallets.Setup(item => item.TransferWallet(It.IsAny<TransferWalletRequest>()))
            .Callback<TransferWalletRequest, CancellationToken>((request, _) => refundRequests.Add(request))
            .ReturnsAsync(() => new CmdResponse
            {
                HttpStatusCode = refundRequests.Count == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK,
                Message = "Synthetic lost reply"
            });
        var service = CreateReturnsService(db, tenantId, inventory, wallets);
        var request = CreateReturnRequest(seed, PosPaymentMethod.WalletTransfer);

        var first = await service.CreateAsync(request, CancellationToken.None);
        first.IsSuccess.Should().BeTrue(first.Message);
        first.Data!.Status.Should().Be(PosReturnStatus.RefundFailed);
        var returnId = first.Data.Id;
        seed.Register.MerchantCredentialId = Guid.NewGuid();
        seed.Register.IsEnabled = false;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var retry = await service.RetryAsync(new RetryPosReturnRequest { ReturnId = returnId }, CancellationToken.None);
        var replay = await service.CreateAsync(request, CancellationToken.None);

        retry.IsSuccess.Should().BeTrue(retry.Message);
        retry.Data!.Status.Should().Be(PosReturnStatus.Completed);
        replay.IsSuccess.Should().BeTrue(replay.Message);
        replay.Data!.Id.Should().Be(returnId);
        refundRequests.Should().HaveCount(2);
        refundRequests.Select(item => item.IdempotencyKey).Distinct().Should().ContainSingle();
        refundRequests.Should().OnlyContain(item =>
            item.CredentialId == seed.Payment.MerchantCredentialId &&
            item.RecipientCredentialId == seed.Payment.CustomerCredentialId &&
            item.WalletTypeId == seed.Payment.WalletTypeId &&
            item.CurrencyId == seed.Payment.CurrencyId &&
            item.Amount == 25 &&
            item.TransferDeductionType == TransferDeductionType.DeductFromSender);
        inventory.Verify(item => item.PostStockMovement(It.IsAny<PostStockMovementRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        (await db.Set<PosReturn>().CountAsync()).Should().Be(1);
        var payment = await db.Set<PosPayment>().SingleAsync(item => item.Id == seed.Payment.Id);
        payment.RefundedAmount.Should().Be(25);
        payment.Status.Should().Be(PosPaymentStatus.Refunded);

        payment.RefundedAmount = 0;
        payment.Status = PosPaymentStatus.Captured;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var completedRetry = await service.RetryAsync(
            new RetryPosReturnRequest { ReturnId = returnId }, CancellationToken.None);
        completedRetry.IsSuccess.Should().BeTrue(completedRetry.Message);
        var reconciledPayment = await db.Set<PosPayment>().SingleAsync(item => item.Id == seed.Payment.Id);
        reconciledPayment.RefundedAmount.Should().Be(25);
        reconciledPayment.Status.Should().Be(PosPaymentStatus.Refunded);
        refundRequests.Should().HaveCount(2, "reconciling a completed return must not post another wallet transfer");
    }

    [Test]
    public async Task RetryReturn_InventoryReplyLost_DoesNotRefundUntilRestockAcknowledged()
    {
        var tenantId = Guid.NewGuid();
        var seed = CreateCompletedSale(tenantId, PosPaymentMethod.CashDrawer);
        await using var db = CreateContext(tenantId);
        db.AddRange(seed.Register, seed.Sale, seed.Line, seed.Payment);
        await db.SaveChangesAsync();
        var movementRequests = new List<PostStockMovementRequest>();
        var inventory = new Mock<IInventarioServiceWrapper>(MockBehavior.Strict);
        inventory.Setup(item => item.PostStockMovement(It.IsAny<PostStockMovementRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PostStockMovementRequest, CancellationToken>((request, _) => movementRequests.Add(request))
            .ReturnsAsync(() => new CmdResponse
            {
                HttpStatusCode = movementRequests.Count == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK
            });
        var wallets = new Mock<IWalletsServiceWrapper>(MockBehavior.Strict);
        wallets.Setup(item => item.DecrementWallet(It.IsAny<DecrementWalletRequest>()))
            .ReturnsAsync(new CmdResponse { HttpStatusCode = HttpStatusCode.OK });
        var service = CreateReturnsService(db, tenantId, inventory, wallets);

        var first = await service.CreateAsync(CreateReturnRequest(seed, PosPaymentMethod.CashDrawer), CancellationToken.None);
        first.IsSuccess.Should().BeTrue(first.Message);
        first.Data!.Status.Should().Be(PosReturnStatus.InventoryPostFailed);
        wallets.Verify(item => item.DecrementWallet(It.IsAny<DecrementWalletRequest>()), Times.Never);
        db.ChangeTracker.Clear();
        var retry = await service.RetryAsync(new RetryPosReturnRequest { ReturnId = first.Data.Id }, CancellationToken.None);

        retry.IsSuccess.Should().BeTrue(retry.Message);
        retry.Data!.Status.Should().Be(PosReturnStatus.Completed);
        movementRequests.Should().HaveCount(2);
        movementRequests.Select(item => item.IdempotencyKey).Distinct().Should().ContainSingle();
        movementRequests.Should().OnlyContain(item => item.Quantity == seed.Line.Quantity &&
            item.ProductId == seed.Line.ProductId && item.WarehouseId == seed.Line.WarehouseId && item.LocationId == seed.Line.LocationId);
        wallets.Verify(item => item.DecrementWallet(It.IsAny<DecrementWalletRequest>()), Times.Once);
    }

    [Test]
    public async Task CreateReturn_ThreePartialReturns_ConservesRoundedRefundAndRejectsOverReturn()
    {
        var tenantId = Guid.NewGuid();
        var seed = CreateCompletedSale(tenantId, PosPaymentMethod.CashDrawer);
        seed.Line.Quantity = 3;
        seed.Line.UnitPrice = 10;
        seed.Line.LineTotal = 30;
        seed.Sale.SubtotalAmount = 30;
        seed.Sale.DiscountAmount = 1;
        seed.Sale.TaxAmount = 0.01m;
        seed.Sale.TotalAmount = 29.01m;
        seed.Payment.Amount = 29.01m;
        await using var db = CreateContext(tenantId);
        db.AddRange(seed.Register, seed.Sale, seed.Line, seed.Payment);
        await db.SaveChangesAsync();
        var inventory = new Mock<IInventarioServiceWrapper>(MockBehavior.Strict);
        inventory.Setup(item => item.PostStockMovement(It.IsAny<PostStockMovementRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse { HttpStatusCode = HttpStatusCode.OK });
        var refundAmounts = new List<decimal>();
        var wallets = new Mock<IWalletsServiceWrapper>(MockBehavior.Strict);
        wallets.Setup(item => item.DecrementWallet(It.IsAny<DecrementWalletRequest>()))
            .Callback<DecrementWalletRequest, CancellationToken>((request, _) => refundAmounts.Add(request.Amount))
            .ReturnsAsync(new CmdResponse { HttpStatusCode = HttpStatusCode.OK });
        var service = CreateReturnsService(db, tenantId, inventory, wallets);

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var request = CreateReturnRequest(seed, PosPaymentMethod.CashDrawer);
            request.Lines[0].Quantity = 1;
            var result = await service.CreateAsync(request, CancellationToken.None);
            if (attempt < 3)
            {
                result.IsSuccess.Should().BeTrue(result.Message);
                result.Data!.Status.Should().Be(PosReturnStatus.Completed);
                var payment = await db.Set<PosPayment>().SingleAsync(item => item.Id == seed.Payment.Id);
                payment.RefundedAmount.Should().Be(refundAmounts.Sum());
                payment.Status.Should().Be(attempt == 2
                    ? PosPaymentStatus.Refunded
                    : PosPaymentStatus.Captured);
            }
            else
            {
                result.IsSuccess.Should().BeFalse();
                result.StatusCode.Should().Be(409);
            }
        }

        refundAmounts.Should().HaveCount(3);
        refundAmounts.Sum().Should().Be(29.01m);
        (await db.Set<PosReturnLine>().SumAsync(item => item.TaxAmount)).Should().Be(0.01m);
        (await db.Set<PosReturnLine>().SumAsync(item => item.Quantity)).Should().Be(3);
        inventory.Verify(item => item.PostStockMovement(It.IsAny<PostStockMovementRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Test]
    public async Task RegisterAndHeldCartSearch_PageBeyondInitialLimit_RemainsReachable()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext(tenantId);
        var registers = Enumerable.Range(1, 51).Select(index =>
        {
            var register = CreateRegister(tenantId);
            register.Name = $"Register {index:D3}";
            register.Code = $"REG-{index:D3}";
            return register;
        }).ToList();
        db.AddRange(registers);

        var now = DateTime.UtcNow;
        var carts = Enumerable.Range(1, 51).Select(index =>
        {
            var cart = CreateCart(tenantId, registers[0].Id, $"hash-{index}");
            cart.Status = PosCartStatus.Suspended;
            cart.CustomerLabel = index == 51 ? "oldest-marker" : $"Customer {index:D3}";
            cart.SuspendedAt = now.AddMinutes(-index);
            cart.ExpiresAt = now.AddDays(1);
            return cart;
        }).ToList();
        db.AddRange(carts);
        await db.SaveChangesAsync();

        var resolver = CreateResolver(tenantId);
        var inventory = new Mock<IInventarioServiceWrapper>(MockBehavior.Strict);
        var wallets = new Mock<IWalletsServiceWrapper>(MockBehavior.Strict);
        var sales = new PosSalesService(db, inventory.Object, wallets.Object, resolver.Object,
            NullLogger<PosSalesService>.Instance);
        var cartService = new PosCartService(db, inventory.Object, sales, resolver.Object,
            NullLogger<PosCartService>.Instance);
        var registerService = new PosRegisterService(
            db,
            resolver.Object,
            new Mock<IIdentityServerServiceWrapper>(MockBehavior.Strict).Object,
            wallets.Object,
            inventory.Object);

        var registerPageTwo = await registerService.SearchAsync(
            new SearchPosRegistersRequest { Page = 2, PageSize = 50 }, CancellationToken.None);
        var exactRegister = await registerService.SearchAsync(
            new SearchPosRegistersRequest { Search = "REG-051", Page = 1, PageSize = 50 }, CancellationToken.None);
        var cartPageTwo = await cartService.SearchAsync(
            new SearchPosCartsRequest { Status = PosCartStatus.Suspended, Page = 2, PageSize = 50 }, CancellationToken.None);
        var exactCart = await cartService.SearchAsync(
            new SearchPosCartsRequest { Status = PosCartStatus.Suspended, Search = "oldest-marker", Page = 1, PageSize = 50 }, CancellationToken.None);

        registerPageTwo.IsSuccess.Should().BeTrue(registerPageTwo.Message);
        registerPageTwo.Data.Should().ContainSingle().Which.Code.Should().Be("REG-051");
        exactRegister.Data.Should().ContainSingle().Which.Code.Should().Be("REG-051");
        cartPageTwo.IsSuccess.Should().BeTrue(cartPageTwo.Message);
        cartPageTwo.Data.Should().ContainSingle().Which.CustomerLabel.Should().Be("oldest-marker");
        exactCart.Data.Should().ContainSingle().Which.CustomerLabel.Should().Be("oldest-marker");
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

    private static Mock<IPosRequestContextResolver> CreateResolver(Guid tenantId)
    {
        var resolver = new Mock<IPosRequestContextResolver>(MockBehavior.Strict);
        resolver.Setup(item => item.Resolve(It.IsAny<RequestBase>(), It.IsAny<Guid?>()))
            .Returns((RequestBase request, Guid? actor) => Result<PosRequestContext>.Success(
                new PosRequestContext(tenantId, actor, request.Metadata, true, false)));
        return resolver;
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
