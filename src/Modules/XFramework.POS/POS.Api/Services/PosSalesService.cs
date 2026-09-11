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
    IPosRequestContextResolver contextResolver,
    ILogger<PosSalesService> logger)
{
    public async Task<Result<PosSaleReceiptResponse>> CheckoutAsync(
        CheckoutPosSaleRequest request,
        CancellationToken ct)
    {
        var contextResult = contextResolver.Resolve(request, request.CashierCredentialId);
        if (!contextResult.IsSuccess)
            return Result<PosSaleReceiptResponse>.Failure(contextResult.Message!, contextResult.StatusCode);

        var idempotencyKey = PosServiceHelpers.NormalizeOptional(request.IdempotencyKey)
            ?? string.Empty;
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Result<PosSaleReceiptResponse>.Failure("Checkout idempotency key is required", 400);

        if (request.Payment.Amount <= 0)
            return Result<PosSaleReceiptResponse>.Failure("POS sale total must be greater than zero", 400);

        var context = contextResult.Data!;
        var tenantId = context.TenantId;
        var requestHash = PosServiceHelpers.BuildSaleRequestHash(request);

        var replay = await LoadSaleByIdempotencyAsync(tenantId, idempotencyKey, tracking: true, ct);
        if (replay is not null)
        {
            if (!string.Equals(replay.RequestHash, requestHash, StringComparison.Ordinal))
                return Result<PosSaleReceiptResponse>.Conflict("Checkout idempotency key was reused with a different payload");

            if (replay.Status == PosSaleStatus.Completed)
                return Result<PosSaleReceiptResponse>.Success(PosServiceHelpers.ToSaleReceiptResponse(replay), "POS sale replayed");

            if (replay.Status == PosSaleStatus.Cancelled)
                return Result<PosSaleReceiptResponse>.Success(PosServiceHelpers.ToSaleReceiptResponse(replay), "POS sale is cancelled");

            var replayRegister = await LoadRegisterAsync(tenantId, replay.RegisterId, ct, requireEnabled: false);
            if (replayRegister is null)
                return Result<PosSaleReceiptResponse>.NotFound("POS register was not found");

            return await ContinueCheckoutAsync(replay, replayRegister, request, context.Metadata, ct, replayed: true);
        }

        var register = await LoadRegisterAsync(tenantId, request.RegisterId, ct);

        if (register is null)
            return Result<PosSaleReceiptResponse>.NotFound("POS register was not found");

        var paymentCustomerCredentialId = request.Payment.CustomerCredentialId ?? request.CustomerCredentialId;
        if (request.Payment.Method == PosPaymentMethod.WalletTransfer &&
            paymentCustomerCredentialId == register.MerchantCredentialId)
        {
            return Result<PosSaleReceiptResponse>.Conflict(
                "Customer wallet must be different from the register merchant wallet");
        }

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
            RequestHash = requestHash,
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
        var allocations = PosServiceHelpers.BuildSaleRefundAllocations(sale);
        sale.TotalAmount = allocations.Values.Sum(allocation => allocation.RefundAmount);

        if (allocations.Values.Any(allocation => allocation.RefundAmount < 0))
            return Result<PosSaleReceiptResponse>.Failure("Sale discounts cannot make a line total negative", 400);

        if (request.Payment.Amount != sale.TotalAmount)
            return Result<PosSaleReceiptResponse>.Conflict("Payment amount does not match sale total");

        db.Set<PosSale>().Add(sale);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var concurrentReplay = await LoadSaleByIdempotencyAsync(tenantId, idempotencyKey, tracking: true, ct);
            if (concurrentReplay is null)
                throw;

            if (!string.Equals(concurrentReplay.RequestHash, requestHash, StringComparison.Ordinal))
                return Result<PosSaleReceiptResponse>.Conflict("Checkout idempotency key was reused with a different payload");

            return await ContinueCheckoutAsync(concurrentReplay, register, request, context.Metadata, ct, replayed: true);
        }

        return await ContinueCheckoutAsync(sale, register, request, context.Metadata, ct, replayed: false);
    }

    private async Task<Result<PosSaleReceiptResponse>> ContinueCheckoutAsync(
        PosSale sale,
        PosRegister register,
        CheckoutPosSaleRequest? request,
        RequestMetadata metadata,
        CancellationToken ct,
        bool replayed)
    {
        if (sale.Status == PosSaleStatus.Completed)
            return Result<PosSaleReceiptResponse>.Success(PosServiceHelpers.ToSaleReceiptResponse(sale), replayed ? "POS sale replayed" : "POS sale completed");

        if (sale.Status == PosSaleStatus.Cancelled)
            return Result<PosSaleReceiptResponse>.Success(PosServiceHelpers.ToSaleReceiptResponse(sale), "POS sale is cancelled");

        if (sale.Status is PosSaleStatus.Draft or PosSaleStatus.ReservingInventory or PosSaleStatus.InventoryReservationFailed)
        {
            var reservationResult = await ReserveSaleInventoryAsync(sale, metadata, ct);
            if (!reservationResult.IsSuccess)
            {
                sale.Status = PosSaleStatus.InventoryReservationFailed;
                sale.FailureReason = reservationResult.Message;
                sale.RecoveryState = "Inventory reservation failed before payment capture.";
                await db.SaveChangesAsync(ct);
                return Result<PosSaleReceiptResponse>.Success(PosServiceHelpers.ToSaleReceiptResponse(sale), reservationResult.Message);
            }
        }

        if (sale.Status is PosSaleStatus.InventoryReserved or PosSaleStatus.PaymentPending)
        {
            sale.Status = PosSaleStatus.PaymentPending;
            var payment = sale.Payments
                .OrderBy(item => item.CreatedAt)
                .FirstOrDefault();
            if (payment is null)
            {
                if (request is null)
                    return Result<PosSaleReceiptResponse>.Conflict("Pending POS sale has no payment snapshot to retry");

                payment = CreatePayment(sale, register, request);
                db.Set<PosPayment>().Add(payment);
                sale.Payments.Add(payment);
                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    db.ChangeTracker.Clear();
                    var concurrentSale = await LoadSaleAsync(sale.TenantId, sale.Id, true, ct);
                    if (concurrentSale?.Payments.Count != 1)
                        throw;

                    return await ContinueCheckoutAsync(concurrentSale, register, request, metadata, ct, replayed: true);
                }
            }

            if (payment.Status == PosPaymentStatus.Failed)
            {
                sale.Status = PosSaleStatus.PaymentFailed;
                sale.FailureReason = payment.FailureReason;
                sale.RecoveryState = HasOutstandingReservations(sale)
                    ? "Payment failed; some inventory reservations still require release. Retry cancellation."
                    : "Payment failed; inventory reservations were released.";
                await db.SaveChangesAsync(ct);
                return Result<PosSaleReceiptResponse>.Success(PosServiceHelpers.ToSaleReceiptResponse(sale), payment.FailureReason);
            }

            if (payment.Status != PosPaymentStatus.Captured)
            {
                var paymentResult = await CapturePaymentAsync(sale.SaleNumber, payment, metadata);
                if (!paymentResult.IsSuccess)
                {
                    if (IsAmbiguousPaymentFailure(paymentResult.StatusCode))
                    {
                        payment.FailureReason = paymentResult.Message;
                        sale.Status = PosSaleStatus.PaymentPending;
                        sale.FailureReason = paymentResult.Message;
                        sale.RecoveryState = "Payment result is unknown; retry payment from this receipt to reuse the original payment reference and same idempotency key.";
                        await db.SaveChangesAsync(ct);
                        return Result<PosSaleReceiptResponse>.Success(
                            PosServiceHelpers.ToSaleReceiptResponse(sale),
                            paymentResult.Message);
                    }

                    payment.Status = PosPaymentStatus.Failed;
                    payment.FailureReason = paymentResult.Message;
                    sale.Status = PosSaleStatus.PaymentFailed;
                    sale.FailureReason = paymentResult.Message;
                    var releaseResult = await ReleaseReservationsAsync(sale, metadata);
                    sale.RecoveryState = releaseResult.IsSuccess
                        ? "Payment failed; inventory reservations were released."
                        : "Payment failed; some inventory reservations could not be released. Retry cancellation.";
                    await db.SaveChangesAsync(ct);
                    return Result<PosSaleReceiptResponse>.Success(PosServiceHelpers.ToSaleReceiptResponse(sale), paymentResult.Message);
                }

                payment.Status = PosPaymentStatus.Captured;
                payment.CapturedAt = DateTime.UtcNow;
            }

            sale.Status = PosSaleStatus.PaymentCaptured;
            sale.FailureReason = null;
            sale.RecoveryState = null;
            await db.SaveChangesAsync(ct);
        }

        if (sale.Status is PosSaleStatus.PaymentCaptured or PosSaleStatus.InventoryFulfillmentFailed)
        {
            var fulfillmentResult = await FulfillReservationsAsync(sale, metadata);
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

            var receipt = PosServiceHelpers.ToSaleReceiptResponse(sale);
            return replayed
                ? Result<PosSaleReceiptResponse>.Success(receipt, "POS sale replay completed")
                : Result<PosSaleReceiptResponse>.Success(receipt, 201, "POS sale completed");
        }

        return Result<PosSaleReceiptResponse>.Success(PosServiceHelpers.ToSaleReceiptResponse(sale), replayed ? "POS sale replayed" : "POS sale accepted");
    }

    public async Task<Result<PosSaleReceiptResponse>> GetAsync(
        GetPosSaleRequest request,
        CancellationToken ct)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess)
            return Result<PosSaleReceiptResponse>.Failure(contextResult.Message!, contextResult.StatusCode);

        var sale = await LoadSaleAsync(contextResult.Data!.TenantId, request.Id, false, ct);
        return sale is null
            ? Result<PosSaleReceiptResponse>.NotFound("POS sale was not found")
            : Result<PosSaleReceiptResponse>.Success(PosServiceHelpers.ToSaleReceiptResponse(sale));
    }

    public async Task<Result<List<PosSaleSummaryResponse>>> SearchAsync(
        SearchPosSalesRequest request,
        CancellationToken ct)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess)
            return Result<List<PosSaleSummaryResponse>>.Failure(contextResult.Message!, contextResult.StatusCode);

        var tenantId = contextResult.Data!.TenantId;
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
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess)
            return Result<PosSaleReceiptResponse>.Failure(contextResult.Message!, contextResult.StatusCode);

        var sale = await LoadSaleAsync(contextResult.Data!.TenantId, request.SaleId, true, ct);
        if (sale is null)
            return Result<PosSaleReceiptResponse>.NotFound("POS sale was not found");

        if (sale.Status == PosSaleStatus.Cancelled)
            return Result<PosSaleReceiptResponse>.Success(PosServiceHelpers.ToSaleReceiptResponse(sale), "POS sale already cancelled");

        var hasSettledOrInFlightPayment = sale.Payments.Any(payment =>
            !payment.IsDeleted &&
            payment.Status is PosPaymentStatus.Pending or PosPaymentStatus.Captured or PosPaymentStatus.Refunded);
        if (hasSettledOrInFlightPayment || sale.Status is
                PosSaleStatus.PaymentPending or
                PosSaleStatus.PaymentCaptured or
                PosSaleStatus.Completed or
                PosSaleStatus.InventoryFulfillmentFailed)
        {
            return Result<PosSaleReceiptResponse>.Conflict(
                "Paid or payment-in-progress sales cannot be cancelled; complete recovery or create a return instead");
        }

        if (sale.Status is not PosSaleStatus.Draft
            and not PosSaleStatus.InventoryReserved
            and not PosSaleStatus.PaymentFailed
            and not PosSaleStatus.InventoryReservationFailed)
        {
            return Result<PosSaleReceiptResponse>.Conflict("POS sale is not in a cancellable state");
        }

        var releaseResult = await ReleaseReservationsAsync(sale, contextResult.Data.Metadata);
        if (!releaseResult.IsSuccess)
        {
            sale.FailureReason = releaseResult.Message;
            sale.RecoveryState = "Cancellation pending; retry after inventory reservations can be released.";
            await db.SaveChangesAsync(ct);
            return Result<PosSaleReceiptResponse>.Failure(releaseResult.Message!, releaseResult.StatusCode);
        }

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
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess)
            return Result<PosSaleReceiptResponse>.Failure(contextResult.Message!, contextResult.StatusCode);

        var sale = await LoadSaleAsync(contextResult.Data!.TenantId, request.SaleId, true, ct);
        if (sale is null)
            return Result<PosSaleReceiptResponse>.NotFound("POS sale was not found");

        if (sale.Status != PosSaleStatus.InventoryFulfillmentFailed)
            return Result<PosSaleReceiptResponse>.Conflict("Only sales with failed inventory fulfillment can be retried");

        var fulfillmentResult = await FulfillReservationsAsync(sale, contextResult.Data.Metadata);
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

    public async Task<Result<PosSaleReceiptResponse>> RetryPaymentAsync(
        RetryPosSalePaymentRequest request,
        CancellationToken ct)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess)
            return Result<PosSaleReceiptResponse>.Failure(contextResult.Message!, contextResult.StatusCode);

        var sale = await LoadSaleAsync(contextResult.Data!.TenantId, request.SaleId, true, ct);
        if (sale is null)
            return Result<PosSaleReceiptResponse>.NotFound("POS sale was not found");

        if (sale.Status != PosSaleStatus.PaymentPending)
            return Result<PosSaleReceiptResponse>.Conflict("Only sales with a pending payment can retry payment");

        var register = await LoadRegisterAsync(sale.TenantId, sale.RegisterId, ct, requireEnabled: false);
        if (register is null)
            return Result<PosSaleReceiptResponse>.NotFound("POS register was not found");

        return await ContinueCheckoutAsync(
            sale,
            register,
            request: null,
            metadata: contextResult.Data.Metadata,
            ct: ct,
            replayed: true);
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
            if (line.ReservationId.HasValue)
                continue;

            var reservationKey = PosServiceHelpers.SaleLineReservationReference(line);
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
                IdempotencyKey = reservationKey,
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

            line.FailureReason = null;
            await db.SaveChangesAsync(ct);
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
            CashTenderedAmount = request.Payment.Method == PosPaymentMethod.CashDrawer
                ? request.Payment.CashTenderedAmount ?? request.Payment.Amount
                : null,
            ChangeAmount = request.Payment.Method == PosPaymentMethod.CashDrawer
                ? (request.Payment.CashTenderedAmount ?? request.Payment.Amount) - request.Payment.Amount
                : null,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid(),
            IsEnabled = true
        };

        payment.ReferenceNumber = PosServiceHelpers.SalePaymentReference(sale);
        payment.IdempotencyKey = payment.ReferenceNumber;
        return payment;
    }

    private async Task<Result> CapturePaymentAsync(
        string saleNumber,
        PosPayment payment,
        RequestMetadata metadata)
    {
        if (payment.Method == PosPaymentMethod.CashDrawer)
        {
            if (payment.WalletId is not { } walletId || walletId == Guid.Empty)
                return Result.Failure("Captured payment is missing its cash drawer wallet", 409);

            var response = await wallets.IncrementWallet(new IncrementWalletRequest
            {
                CredentialId = payment.MerchantCredentialId,
                WalletId = walletId,
                WalletTypeId = payment.WalletTypeId,
                CurrencyId = payment.CurrencyId,
                Amount = payment.Amount,
                Remarks = $"POS sale {saleNumber}",
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

        if (customerCredentialId == payment.MerchantCredentialId)
            return Result.Failure("Customer wallet must be different from the register merchant wallet", 409);

        var transfer = await wallets.TransferWallet(new TransferWalletRequest
        {
            CredentialId = customerCredentialId,
            RecipientCredentialId = payment.MerchantCredentialId,
            WalletTypeId = payment.WalletTypeId,
            CurrencyId = payment.CurrencyId,
            Amount = payment.Amount,
            Remarks = $"POS sale {saleNumber}",
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

    private async Task<Result> ReleaseReservationsAsync(
        PosSale sale,
        RequestMetadata metadata)
    {
        Result? firstFailure = null;
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
                firstFailure ??= Result.Failure(line.FailureReason, (int)response.HttpStatusCode);
                logger.LogWarning(
                    "POS sale {SaleId} line {LineId} reservation release failed: {Message}",
                    sale.Id,
                    line.Id,
                    line.FailureReason);
            }
            else
            {
                line.ReservationId = null;
                line.FailureReason = null;
            }
        }

        return firstFailure ?? Result.Success();
    }

    private async Task<PosRegister?> LoadRegisterAsync(
        Guid tenantId,
        Guid registerId,
        CancellationToken ct,
        bool requireEnabled = true)
    {
        var query = db.Set<PosRegister>()
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                item.Id == registerId &&
                !item.IsDeleted);

        if (requireEnabled)
            query = query.Where(item => item.IsEnabled);

        return await query.FirstOrDefaultAsync(ct);
    }

    private static bool HasOutstandingReservations(PosSale sale) =>
        sale.Lines.Any(item => item.ReservationId.HasValue && item.FulfilledAt is null);

    private static bool IsAmbiguousPaymentFailure(int statusCode) =>
        statusCode <= 0 || statusCode is 408 or 429 || statusCode >= 500;

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
        bool tracking,
        CancellationToken ct) =>
        await (tracking
                ? db.Set<PosSale>().AsTracking()
                : db.Set<PosSale>().AsNoTracking())
            .Include(item => item.Lines)
            .Include(item => item.Payments)
            .FirstOrDefaultAsync(item =>
                    item.TenantId == tenantId &&
                    item.IdempotencyKey == idempotencyKey &&
                    !item.IsDeleted,
                ct);
}
