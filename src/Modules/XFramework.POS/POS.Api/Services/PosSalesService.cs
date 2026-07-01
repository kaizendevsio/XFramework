using Inventario.Integration.Drivers;
using Microsoft.EntityFrameworkCore;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Contracts.Responses;
using POS.Domain.Shared.Enums;
using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Integration.Drivers;
using XFramework.Core.Patterns;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Enums;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Products;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;

namespace POS.Api.Services;

public sealed class PosSalesService(
    AppDbContext db,
    IInventarioServiceWrapper inventario,
    IWalletsServiceWrapper wallets,
    ILogger<PosSalesService> logger)
{
    public async Task<Result<PosSaleReceiptResponse>> CheckoutAsync(
        CheckoutPosSaleRequest request,
        CancellationToken ct)
    {
        if (!PosServiceHelpers.TryResolveTenantId(request.Metadata, out var tenantId))
            return Result<PosSaleReceiptResponse>.Failure("Tenant ID is required", 400);

        var idempotencyKey = PosServiceHelpers.NormalizeOptional(request.IdempotencyKey)
            ?? $"POS.Checkout.{Guid.NewGuid():N}";

        var replay = await LoadSaleByIdempotencyAsync(tenantId, idempotencyKey, ct);
        if (replay is not null)
            return Result<PosSaleReceiptResponse>.Success(PosServiceHelpers.ToSaleReceiptResponse(replay), "POS sale replayed");

        var register = await db.Set<PosRegister>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                item.Id == request.RegisterId &&
                item.IsEnabled &&
                !item.IsDeleted,
                ct);

        if (register is null)
            return Result<PosSaleReceiptResponse>.NotFound("POS register was not found");

        if (request.Lines.Count == 0)
            return Result<PosSaleReceiptResponse>.Failure("At least one sale line is required", 400);

        var now = DateTime.UtcNow;
        var sale = new PosSale
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SaleNumber = string.Empty,
            RegisterId = register.Id,
            CashierCredentialId = request.CashierCredentialId,
            CustomerCredentialId = request.CustomerCredentialId,
            WarehouseId = request.WarehouseId ?? register.DefaultWarehouseId,
            LocationId = request.LocationId ?? register.DefaultLocationId,
            CurrencyId = request.CurrencyId ?? register.CurrencyId,
            WalletTypeId = request.WalletTypeId ?? register.WalletTypeId,
            Status = PosSaleStatus.Draft,
            PaymentMethod = request.Payment.Method,
            IdempotencyKey = idempotencyKey,
            DiscountAmount = request.DiscountAmount,
            TaxAmount = request.TaxAmount,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid(),
            IsEnabled = true
        };
        sale.SaleNumber = PosServiceHelpers.NewSaleNumber(now, sale.Id);

        var lineBuildResult = await BuildSaleLinesAsync(sale, request, register, ct);
        if (!lineBuildResult.IsSuccess)
            return Result<PosSaleReceiptResponse>.Failure(lineBuildResult.Message!, lineBuildResult.StatusCode);

        sale.Lines = lineBuildResult.Data!;
        sale.SubtotalAmount = sale.Lines.Sum(line => line.Quantity * line.UnitPrice);
        sale.TotalAmount = sale.SubtotalAmount - sale.DiscountAmount + sale.TaxAmount;

        if (sale.TotalAmount < 0)
            return Result<PosSaleReceiptResponse>.Failure("Sale total cannot be negative", 400);

        if (request.Payment.Amount != sale.TotalAmount)
            return Result<PosSaleReceiptResponse>.Conflict("Payment amount does not match sale total");

        db.Set<PosSale>().Add(sale);
        await db.SaveChangesAsync(ct);

        var reservationResult = await ReserveSaleInventoryAsync(sale, request.Metadata, ct);
        if (!reservationResult.IsSuccess)
        {
            sale.Status = PosSaleStatus.InventoryReservationFailed;
            sale.FailureReason = reservationResult.Message;
            sale.RecoveryState = "Inventory reservation failed before payment capture.";
            await db.SaveChangesAsync(ct);
            return Result<PosSaleReceiptResponse>.Success(PosServiceHelpers.ToSaleReceiptResponse(sale), reservationResult.Message);
        }

        sale.Status = PosSaleStatus.PaymentPending;
        var payment = CreatePayment(sale, register, request);
        db.Set<PosPayment>().Add(payment);
        await db.SaveChangesAsync(ct);

        var paymentResult = await CapturePaymentAsync(register, sale, payment, request.Metadata);
        if (!paymentResult.IsSuccess)
        {
            payment.Status = PosPaymentStatus.Failed;
            payment.FailureReason = paymentResult.Message;
            sale.Status = PosSaleStatus.PaymentFailed;
            sale.FailureReason = paymentResult.Message;
            sale.RecoveryState = "Payment failed; inventory reservations were released.";
            await ReleaseReservationsAsync(sale, request.Metadata);
            await db.SaveChangesAsync(ct);
            return Result<PosSaleReceiptResponse>.Success(PosServiceHelpers.ToSaleReceiptResponse(sale), paymentResult.Message);
        }

        payment.Status = PosPaymentStatus.Captured;
        payment.CapturedAt = DateTime.UtcNow;
        sale.Status = PosSaleStatus.PaymentCaptured;
        await db.SaveChangesAsync(ct);

        var fulfillmentResult = await FulfillReservationsAsync(sale, request.Metadata);
        if (!fulfillmentResult.IsSuccess)
        {
            sale.Status = PosSaleStatus.InventoryFulfillmentFailed;
            sale.FailureReason = fulfillmentResult.Message;
            sale.RecoveryState = "Payment captured; retry fulfillment for unfulfilled reservation IDs.";
            await db.SaveChangesAsync(ct);
            return Result<PosSaleReceiptResponse>.Success(PosServiceHelpers.ToSaleReceiptResponse(sale), fulfillmentResult.Message);
        }

        sale.Status = PosSaleStatus.Completed;
        sale.CompletedAt = DateTime.UtcNow;
        sale.FailureReason = null;
        sale.RecoveryState = null;
        await db.SaveChangesAsync(ct);

        return Result<PosSaleReceiptResponse>.Success(PosServiceHelpers.ToSaleReceiptResponse(sale), 201, "POS sale completed");
    }

    public async Task<Result<PosSaleReceiptResponse>> GetAsync(
        GetPosSaleRequest request,
        CancellationToken ct)
    {
        if (!PosServiceHelpers.TryResolveTenantId(request.Metadata, out var tenantId))
            return Result<PosSaleReceiptResponse>.Failure("Tenant ID is required", 400);

        var sale = await LoadSaleAsync(tenantId, request.Id, false, ct);
        return sale is null
            ? Result<PosSaleReceiptResponse>.NotFound("POS sale was not found")
            : Result<PosSaleReceiptResponse>.Success(PosServiceHelpers.ToSaleReceiptResponse(sale));
    }

    public async Task<Result<List<PosSaleSummaryResponse>>> SearchAsync(
        SearchPosSalesRequest request,
        CancellationToken ct)
    {
        if (!PosServiceHelpers.TryResolveTenantId(request.Metadata, out var tenantId))
            return Result<List<PosSaleSummaryResponse>>.Failure("Tenant ID is required", 400);

        var (page, pageSize) = PosServiceHelpers.NormalizePage(request.Page, request.PageSize);
        IQueryable<PosSale> query = db.Set<PosSale>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted);

        if (request.RegisterId.HasValue)
            query = query.Where(item => item.RegisterId == request.RegisterId.Value);

        if (request.Status.HasValue)
            query = query.Where(item => item.Status == request.Status.Value);

        if (request.From.HasValue)
            query = query.Where(item => item.CreatedAt >= request.From.Value);

        if (request.To.HasValue)
            query = query.Where(item => item.CreatedAt <= request.To.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item =>
                item.SaleNumber.Contains(search) ||
                item.IdempotencyKey.Contains(search));
        }

        var items = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Result<List<PosSaleSummaryResponse>>.Success(
            items.Select(PosServiceHelpers.ToSaleSummaryResponse).ToList());
    }

    public async Task<Result<PosSaleReceiptResponse>> CancelAsync(
        CancelPosSaleRequest request,
        CancellationToken ct)
    {
        if (!PosServiceHelpers.TryResolveTenantId(request.Metadata, out var tenantId))
            return Result<PosSaleReceiptResponse>.Failure("Tenant ID is required", 400);

        var sale = await LoadSaleAsync(tenantId, request.SaleId, true, ct);
        if (sale is null)
            return Result<PosSaleReceiptResponse>.NotFound("POS sale was not found");

        if (sale.Status is PosSaleStatus.Completed or PosSaleStatus.PaymentCaptured)
            return Result<PosSaleReceiptResponse>.Conflict("Completed or paid sales cannot be cancelled; create a return instead");

        await ReleaseReservationsAsync(sale, request.Metadata);
        sale.Status = PosSaleStatus.Cancelled;
        sale.CancelledAt = DateTime.UtcNow;
        sale.FailureReason = PosServiceHelpers.NormalizeOptional(request.Reason);
        sale.RecoveryState = null;
        await db.SaveChangesAsync(ct);

        return Result<PosSaleReceiptResponse>.Success(PosServiceHelpers.ToSaleReceiptResponse(sale), "POS sale cancelled");
    }

    public async Task<Result<PosSaleReceiptResponse>> RetryFulfillmentAsync(
        RetryPosSaleFulfillmentRequest request,
        CancellationToken ct)
    {
        if (!PosServiceHelpers.TryResolveTenantId(request.Metadata, out var tenantId))
            return Result<PosSaleReceiptResponse>.Failure("Tenant ID is required", 400);

        var sale = await LoadSaleAsync(tenantId, request.SaleId, true, ct);
        if (sale is null)
            return Result<PosSaleReceiptResponse>.NotFound("POS sale was not found");

        if (sale.Status != PosSaleStatus.InventoryFulfillmentFailed)
            return Result<PosSaleReceiptResponse>.Conflict("Only sales with failed inventory fulfillment can be retried");

        var fulfillmentResult = await FulfillReservationsAsync(sale, request.Metadata);
        if (!fulfillmentResult.IsSuccess)
        {
            sale.FailureReason = fulfillmentResult.Message;
            sale.RecoveryState = "Payment captured; retry fulfillment for unfulfilled reservation IDs.";
            await db.SaveChangesAsync(ct);
            return Result<PosSaleReceiptResponse>.Success(PosServiceHelpers.ToSaleReceiptResponse(sale), fulfillmentResult.Message);
        }

        sale.Status = PosSaleStatus.Completed;
        sale.CompletedAt = DateTime.UtcNow;
        sale.FailureReason = null;
        sale.RecoveryState = null;
        await db.SaveChangesAsync(ct);

        return Result<PosSaleReceiptResponse>.Success(PosServiceHelpers.ToSaleReceiptResponse(sale), "POS sale fulfillment completed");
    }

    private async Task<Result<List<PosSaleLine>>> BuildSaleLinesAsync(
        PosSale sale,
        CheckoutPosSaleRequest request,
        PosRegister register,
        CancellationToken ct)
    {
        var lines = new List<PosSaleLine>();
        var lineNumber = 1;

        foreach (var requestLine in request.Lines)
        {
            var productResponse = await inventario.GetSellableProduct(new GetSellableProductRequest
            {
                ProductId = requestLine.ProductId,
                Metadata = request.Metadata
            });

            if (!productResponse.IsSuccess || productResponse.Response is null)
                return Result<List<PosSaleLine>>.Failure(
                    productResponse.Message ?? "Inventario product lookup failed",
                    (int)productResponse.HttpStatusCode);

            var product = productResponse.Response;
            var variation = requestLine.ProductVariationId.HasValue
                ? product.Variations.FirstOrDefault(item => item.ProductVariationId == requestLine.ProductVariationId.Value)
                : null;

            if (requestLine.ProductVariationId.HasValue && variation is null)
                return Result<List<PosSaleLine>>.NotFound("Product variation was not found");

            var currentPrice = variation?.Price ?? product.Price;
            if (currentPrice != requestLine.ExpectedUnitPrice)
                return Result<List<PosSaleLine>>.Conflict("Catalog price changed before checkout");

            var quantity = requestLine.Quantity;
            var lineTotal = quantity * currentPrice - requestLine.DiscountAmount + requestLine.TaxAmount;
            if (lineTotal < 0)
                return Result<List<PosSaleLine>>.Failure("Line total cannot be negative", 400);

            lines.Add(new PosSaleLine
            {
                Id = Guid.NewGuid(),
                TenantId = sale.TenantId,
                SaleId = sale.Id,
                LineNumber = lineNumber++,
                ProductId = requestLine.ProductId,
                ProductVariationId = requestLine.ProductVariationId,
                ProductName = product.Name,
                VariantName = variation?.VariantName,
                SKU = product.SKU,
                Quantity = quantity,
                UnitPrice = currentPrice,
                ExpectedUnitPrice = requestLine.ExpectedUnitPrice,
                DiscountAmount = requestLine.DiscountAmount,
                TaxAmount = requestLine.TaxAmount,
                LineTotal = lineTotal,
                WarehouseId = requestLine.WarehouseId ?? request.WarehouseId ?? register.DefaultWarehouseId,
                LocationId = requestLine.LocationId ?? request.LocationId ?? register.DefaultLocationId,
                LotId = requestLine.LotId,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid(),
                IsEnabled = true
            });
        }

        return Result<List<PosSaleLine>>.Success(lines);
    }

    private async Task<Result> ReserveSaleInventoryAsync(
        PosSale sale,
        RequestMetadata metadata,
        CancellationToken ct)
    {
        sale.Status = PosSaleStatus.ReservingInventory;
        await db.SaveChangesAsync(ct);

        foreach (var line in sale.Lines.OrderBy(item => item.LineNumber))
        {
            var reservationResponse = await inventario.ReserveInventory(new ReserveInventoryRequest
            {
                ProductId = line.ProductId,
                ProductVariationId = line.ProductVariationId,
                WarehouseId = line.WarehouseId,
                LocationId = line.LocationId,
                LotId = line.LotId,
                Quantity = line.Quantity,
                ReferenceType = PosServiceHelpers.SaleLineReferenceType,
                ReferenceId = line.Id,
                Reason = $"POS sale {sale.SaleNumber}",
                Metadata = metadata
            });

            if (!reservationResponse.IsSuccess)
            {
                await ReleaseReservationsAsync(sale, metadata);
                return Result.Failure(reservationResponse.Message ?? "Inventory reservation failed", (int)reservationResponse.HttpStatusCode);
            }

            var reservationLookup = await inventario.GetReservations(new GetReservationsRequest
            {
                ProductId = line.ProductId,
                ProductVariationId = line.ProductVariationId,
                ReferenceType = PosServiceHelpers.SaleLineReferenceType,
                ReferenceId = line.Id,
                Metadata = metadata
            });

            var reservation = reservationLookup.Response?
                .OrderByDescending(item => item.ReservedAt)
                .FirstOrDefault();

            line.ReservationId = reservation?.Id;
            if (line.ReservationId is null)
            {
                await ReleaseReservationsAsync(sale, metadata);
                return Result.Failure("Inventory reservation succeeded but no reservation ID was returned", 502);
            }
        }

        sale.Status = PosSaleStatus.InventoryReserved;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private PosPayment CreatePayment(
        PosSale sale,
        PosRegister register,
        CheckoutPosSaleRequest request)
    {
        var payment = new PosPayment
        {
            Id = Guid.NewGuid(),
            TenantId = sale.TenantId,
            SaleId = sale.Id,
            Method = request.Payment.Method,
            Status = PosPaymentStatus.Pending,
            Amount = request.Payment.Amount,
            CurrencyId = sale.CurrencyId,
            WalletTypeId = sale.WalletTypeId,
            WalletId = request.Payment.Method == PosPaymentMethod.CashDrawer ? register.CashDrawerWalletId : null,
            CustomerCredentialId = request.Payment.CustomerCredentialId ?? request.CustomerCredentialId,
            MerchantCredentialId = register.MerchantCredentialId,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid(),
            IsEnabled = true
        };

        payment.ReferenceNumber = PosServiceHelpers.SalePaymentReference(sale, payment);
        payment.IdempotencyKey = payment.ReferenceNumber;
        return payment;
    }

    private async Task<Result> CapturePaymentAsync(
        PosRegister register,
        PosSale sale,
        PosPayment payment,
        RequestMetadata metadata)
    {
        if (payment.Method == PosPaymentMethod.CashDrawer)
        {
            var response = await wallets.IncrementWallet(new IncrementWalletRequest
            {
                CredentialId = register.MerchantCredentialId,
                WalletId = register.CashDrawerWalletId,
                WalletTypeId = sale.WalletTypeId,
                CurrencyId = sale.CurrencyId,
                Amount = payment.Amount,
                Remarks = $"POS sale {sale.SaleNumber}",
                ReferenceNumber = payment.ReferenceNumber,
                IdempotencyKey = payment.IdempotencyKey,
                Metadata = metadata
            });

            return response.IsSuccess
                ? Result.Success()
                : Result.Failure(response.Message ?? "Cash drawer wallet increment failed", (int)response.HttpStatusCode);
        }

        var customerCredentialId = payment.CustomerCredentialId ?? Guid.Empty;
        if (customerCredentialId == Guid.Empty)
            return Result.Failure("Customer credential is required for wallet transfer payments", 400);

        var transfer = await wallets.TransferWallet(new TransferWalletRequest
        {
            CredentialId = customerCredentialId,
            RecipientCredentialId = register.MerchantCredentialId,
            WalletTypeId = sale.WalletTypeId,
            CurrencyId = sale.CurrencyId,
            Amount = payment.Amount,
            Remarks = $"POS sale {sale.SaleNumber}",
            ReferenceNumber = payment.ReferenceNumber,
            IdempotencyKey = payment.IdempotencyKey,
            TransactionPurpose = TransactionPurpose.Payment,
            Metadata = metadata
        });

        return transfer.IsSuccess
            ? Result.Success()
            : Result.Failure(transfer.Message ?? "Wallet transfer payment failed", (int)transfer.HttpStatusCode);
    }

    private async Task<Result> FulfillReservationsAsync(
        PosSale sale,
        RequestMetadata metadata)
    {
        foreach (var line in sale.Lines
                     .Where(item => item.ReservationId.HasValue && item.FulfilledAt is null)
                     .OrderBy(item => item.LineNumber))
        {
            var response = await inventario.FulfillReservation(new FulfillReservationRequest
            {
                ReservationId = line.ReservationId!.Value,
                Reason = $"POS sale {sale.SaleNumber}",
                Metadata = metadata
            });

            if (!response.IsSuccess)
            {
                line.FailureReason = response.Message ?? "Inventory fulfillment failed";
                logger.LogWarning(
                    "POS sale {SaleId} line {LineId} fulfillment failed: {Message}",
                    sale.Id,
                    line.Id,
                    line.FailureReason);
                return Result.Failure(line.FailureReason, (int)response.HttpStatusCode);
            }

            line.FulfilledAt = DateTime.UtcNow;
            line.FailureReason = null;
        }

        return Result.Success();
    }

    private async Task ReleaseReservationsAsync(
        PosSale sale,
        RequestMetadata metadata)
    {
        foreach (var line in sale.Lines.Where(item => item.ReservationId.HasValue && item.FulfilledAt is null))
        {
            var response = await inventario.ReleaseReservation(new ReleaseReservationRequest
            {
                ReservationId = line.ReservationId!.Value,
                Reason = $"POS sale {sale.SaleNumber} released",
                Metadata = metadata
            });

            if (!response.IsSuccess)
            {
                line.FailureReason = response.Message ?? "Inventory reservation release failed";
                logger.LogWarning(
                    "POS sale {SaleId} line {LineId} reservation release failed: {Message}",
                    sale.Id,
                    line.Id,
                    line.FailureReason);
            }
            else
            {
                line.ReservationId = null;
            }
        }
    }

    private async Task<PosSale?> LoadSaleAsync(
        Guid tenantId,
        Guid saleId,
        bool tracking,
        CancellationToken ct)
    {
        var query = db.Set<PosSale>()
            .Include(item => item.Lines)
            .Include(item => item.Payments)
            .Where(item => item.TenantId == tenantId && item.Id == saleId && !item.IsDeleted);

        if (!tracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(ct);
    }

    private async Task<PosSale?> LoadSaleByIdempotencyAsync(
        Guid tenantId,
        string idempotencyKey,
        CancellationToken ct) =>
        await db.Set<PosSale>()
            .AsNoTracking()
            .Include(item => item.Lines)
            .Include(item => item.Payments)
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                item.IdempotencyKey == idempotencyKey &&
                !item.IsDeleted,
                ct);
}
