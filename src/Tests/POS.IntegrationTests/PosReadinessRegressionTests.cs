using System.Net;
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
using Wallets.Integration.Drivers;
using XFramework.Core.Patterns;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;
using XFramework.TestInfrastructure;

namespace POS.IntegrationTests;

[TestFixture]
[Category(TestCategories.POS)]
[Category(TestCategories.Integration)]
public sealed class PosReadinessRegressionTests
{
    private PostgreSqlContainer postgres = null!;
    private DbContextOptions<AppDbContext> options = null!;

    [OneTimeSetUp]
    public async Task StartDatabase()
    {
        postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("pos_readiness").WithUsername("qa").WithPassword("isolated_test_only").Build();
        await postgres.StartAsync();
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(PosRegister).TypeHandle);
        options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(postgres.GetConnectionString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;
        await using var db = Context(Guid.NewGuid());
        await db.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public async Task StopDatabase()
    {
        if (postgres is not null) await postgres.DisposeAsync();
    }

    [Test]
    public async Task CancelAsync_CapturedPaymentWithFailedFulfillment_DoesNotCancelPaidSale()
    {
        var tenant = Guid.NewGuid();
        await using var db = Context(tenant);
        var sale = Sale(tenant, PosSaleStatus.InventoryFulfillmentFailed);
        sale.Payments.Add(new PosPayment
        {
            Id = Guid.NewGuid(), TenantId = tenant, SaleId = sale.Id,
            Method = PosPaymentMethod.CashDrawer, Status = PosPaymentStatus.Captured,
            Amount = 10, CurrencyId = sale.CurrencyId, WalletTypeId = sale.WalletTypeId,
            MerchantCredentialId = sale.Register.MerchantCredentialId,
            ReferenceNumber = Guid.NewGuid().ToString("N"), IdempotencyKey = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow, ConcurrencyStamp = Guid.NewGuid(), IsEnabled = true
        });
        db.Add(sale);
        await db.SaveChangesAsync();
        var service = Service(db, tenant, new Mock<IInventarioServiceWrapper>(MockBehavior.Strict));

        var result = await service.CancelAsync(new CancelPosSaleRequest { SaleId = sale.Id }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse("captured money requires fulfillment recovery or an explicit refund");
        db.ChangeTracker.Clear();
        (await db.Set<PosSale>().SingleAsync(s => s.Id == sale.Id)).Status
            .Should().Be(PosSaleStatus.InventoryFulfillmentFailed);
    }

    [Test]
    public async Task CancelAsync_ReservationReleaseFails_DoesNotClaimCancellationSucceeded()
    {
        var tenant = Guid.NewGuid();
        await using var db = Context(tenant);
        var sale = Sale(tenant, PosSaleStatus.InventoryReserved);
        sale.Lines.Add(new PosSaleLine
        {
            Id = Guid.NewGuid(), TenantId = tenant, SaleId = sale.Id, ProductId = Guid.NewGuid(),
            ProductName = "Synthetic item", Quantity = 1, UnitPrice = 10, LineTotal = 10,
            WarehouseId = sale.WarehouseId, LocationId = sale.LocationId, ReservationId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow, ConcurrencyStamp = Guid.NewGuid(), IsEnabled = true
        });
        db.Add(sale);
        await db.SaveChangesAsync();
        var inventory = new Mock<IInventarioServiceWrapper>(MockBehavior.Strict);
        inventory.Setup(i => i.ReleaseReservation(It.IsAny<ReleaseReservationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse { HttpStatusCode = HttpStatusCode.ServiceUnavailable, Message = "Injected inventory outage" });
        var service = Service(db, tenant, inventory);

        var result = await service.CancelAsync(new CancelPosSaleRequest { SaleId = sale.Id }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse("stock remains reserved and the operator must be able to retry release");
        db.ChangeTracker.Clear();
        (await db.Set<PosSale>().SingleAsync(s => s.Id == sale.Id)).Status.Should().NotBe(PosSaleStatus.Cancelled);
    }

    [Test]
    public async Task CancelAsync_PendingPayment_DoesNotReleaseInventoryOrCancelSale()
    {
        var tenant = Guid.NewGuid();
        await using var db = Context(tenant);
        var sale = Sale(tenant, PosSaleStatus.PaymentPending);
        sale.Lines.Add(Line(sale));
        sale.Payments.Add(Payment(sale, PosPaymentStatus.Pending));
        db.Add(sale);
        await db.SaveChangesAsync();
        var inventory = new Mock<IInventarioServiceWrapper>(MockBehavior.Strict);
        var service = Service(db, tenant, inventory);

        var result = await service.CancelAsync(new CancelPosSaleRequest { SaleId = sale.Id }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse("payment capture may still be in flight");
        inventory.VerifyNoOtherCalls();
        db.ChangeTracker.Clear();
        var persisted = await db.Set<PosSale>().Include(item => item.Lines).SingleAsync(item => item.Id == sale.Id);
        persisted.Status.Should().Be(PosSaleStatus.PaymentPending);
        persisted.Lines.Should().ContainSingle().Which.ReservationId.Should().NotBeNull();
    }

    [Test]
    public async Task CancelAsync_PartialReleaseFailure_RetryReleasesOnlyRemainingReservationThenCancels()
    {
        var tenant = Guid.NewGuid();
        await using var db = Context(tenant);
        var sale = Sale(tenant, PosSaleStatus.InventoryReserved);
        var firstLine = Line(sale);
        var secondLine = Line(sale);
        firstLine.LineNumber = 1;
        secondLine.LineNumber = 2;
        var firstReservationId = firstLine.ReservationId;
        var secondReservationId = secondLine.ReservationId;
        sale.Lines.Add(firstLine);
        sale.Lines.Add(secondLine);
        db.Add(sale);
        await db.SaveChangesAsync();

        var inventory = new Mock<IInventarioServiceWrapper>(MockBehavior.Strict);
        inventory.Setup(item => item.ReleaseReservation(
                It.Is<ReleaseReservationRequest>(request => request.ReservationId == firstReservationId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse { HttpStatusCode = HttpStatusCode.OK });
        inventory.SetupSequence(item => item.ReleaseReservation(
                It.Is<ReleaseReservationRequest>(request => request.ReservationId == secondReservationId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CmdResponse { HttpStatusCode = HttpStatusCode.ServiceUnavailable, Message = "Injected inventory outage" })
            .ReturnsAsync(new CmdResponse { HttpStatusCode = HttpStatusCode.OK });
        var service = Service(db, tenant, inventory);

        var firstAttempt = await service.CancelAsync(
            new CancelPosSaleRequest { SaleId = sale.Id }, CancellationToken.None);
        var retry = await service.CancelAsync(
            new CancelPosSaleRequest { SaleId = sale.Id }, CancellationToken.None);

        firstAttempt.IsSuccess.Should().BeFalse();
        retry.IsSuccess.Should().BeTrue(retry.Message);
        retry.Data!.Status.Should().Be(PosSaleStatus.Cancelled);
        inventory.Verify(item => item.ReleaseReservation(
            It.Is<ReleaseReservationRequest>(request => request.ReservationId == firstReservationId),
            It.IsAny<CancellationToken>()), Times.Once);
        inventory.Verify(item => item.ReleaseReservation(
            It.Is<ReleaseReservationRequest>(request => request.ReservationId == secondReservationId),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        inventory.VerifyNoOtherCalls();
        db.ChangeTracker.Clear();
        var persisted = await db.Set<PosSale>().Include(item => item.Lines).SingleAsync(item => item.Id == sale.Id);
        persisted.Status.Should().Be(PosSaleStatus.Cancelled);
        persisted.Lines.Should().OnlyContain(line => line.ReservationId == null);
    }

    [Test]
    public async Task CheckoutAsync_MerchantSelectedAsWalletCustomer_RejectsBeforeSaleOrReservationWrites()
    {
        var tenant = Guid.NewGuid();
        await using var db = Context(tenant);
        var register = Sale(tenant, PosSaleStatus.Draft).Register;
        db.Add(register);
        await db.SaveChangesAsync();
        var inventory = new Mock<IInventarioServiceWrapper>(MockBehavior.Strict);
        var service = Service(db, tenant, inventory);

        var result = await service.CheckoutAsync(new CheckoutPosSaleRequest
        {
            RegisterId = register.Id,
            CashierCredentialId = Guid.NewGuid(),
            CustomerCredentialId = register.MerchantCredentialId,
            IdempotencyKey = $"self-transfer-{Guid.NewGuid():N}",
            Payment = new CheckoutPosPaymentRequest
            {
                Method = PosPaymentMethod.WalletTransfer,
                Amount = 10,
                CustomerCredentialId = register.MerchantCredentialId
            },
            Lines =
            [
                new CheckoutPosSaleLineRequest
                {
                    ProductId = Guid.NewGuid(),
                    Quantity = 1,
                    ExpectedUnitPrice = 10
                }
            ]
        }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        result.Message.Should().Be("Customer wallet must be different from the register merchant wallet");
        (await db.Set<PosSale>().CountAsync()).Should().Be(0);
        inventory.VerifyNoOtherCalls();
    }

    private AppDbContext Context(Guid tenant) => new(options, new HttpContextAccessor(),
        new ConfigurationBuilder().Build(), new TestEffectiveTenantContextAccessor(tenant));

    private static PosSalesService Service(AppDbContext db, Guid tenant, Mock<IInventarioServiceWrapper> inventory)
    {
        var resolver = new Mock<IPosRequestContextResolver>();
        resolver.Setup(r => r.Resolve(It.IsAny<RequestBase>(), It.IsAny<Guid?>()))
            .Returns((RequestBase request, Guid? actor) => Result<PosRequestContext>.Success(
                new PosRequestContext(tenant, actor, request.Metadata, true, false)));
        return new PosSalesService(db, inventory.Object, new Mock<IWalletsServiceWrapper>(MockBehavior.Strict).Object,
            resolver.Object, NullLogger<PosSalesService>.Instance);
    }

    private static PosSale Sale(Guid tenant, PosSaleStatus status)
    {
        var register = new PosRegister
        {
            Id = Guid.NewGuid(), TenantId = tenant, Name = "QA register", MerchantCredentialId = Guid.NewGuid(),
            CashDrawerWalletId = Guid.NewGuid(), WalletTypeId = Guid.NewGuid(), CurrencyId = Guid.NewGuid(),
            DefaultWarehouseId = Guid.NewGuid(), DefaultLocationId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow, ConcurrencyStamp = Guid.NewGuid(), IsEnabled = true
        };
        return new PosSale
        {
            Id = Guid.NewGuid(), TenantId = tenant, Register = register, RegisterId = register.Id,
            SaleNumber = Guid.NewGuid().ToString("N"), IdempotencyKey = Guid.NewGuid().ToString("N"),
            CashierCredentialId = Guid.NewGuid(), WarehouseId = register.DefaultWarehouseId,
            LocationId = register.DefaultLocationId, CurrencyId = register.CurrencyId, WalletTypeId = register.WalletTypeId,
            Status = status, TotalAmount = 10, SubtotalAmount = 10, CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid(), IsEnabled = true
        };
    }

    private static PosSaleLine Line(PosSale sale) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = sale.TenantId,
        SaleId = sale.Id,
        ProductId = Guid.NewGuid(),
        ProductName = "Synthetic item",
        Quantity = 1,
        UnitPrice = 10,
        LineTotal = 10,
        WarehouseId = sale.WarehouseId,
        LocationId = sale.LocationId,
        ReservationId = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid(),
        IsEnabled = true
    };

    private static PosPayment Payment(PosSale sale, PosPaymentStatus status) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = sale.TenantId,
        SaleId = sale.Id,
        Method = PosPaymentMethod.CashDrawer,
        Status = status,
        Amount = sale.TotalAmount,
        CurrencyId = sale.CurrencyId,
        WalletTypeId = sale.WalletTypeId,
        WalletId = sale.Register.CashDrawerWalletId,
        MerchantCredentialId = sale.Register.MerchantCredentialId,
        ReferenceNumber = Guid.NewGuid().ToString("N"),
        IdempotencyKey = Guid.NewGuid().ToString("N"),
        CreatedAt = DateTime.UtcNow,
        ConcurrencyStamp = Guid.NewGuid(),
        IsEnabled = true
    };
}
