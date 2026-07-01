using Inventario.Integration.Drivers;
using Microsoft.EntityFrameworkCore;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Contracts.Responses;
using POS.Domain.Shared.Enums;
using XFramework.Core.Patterns;
using XFramework.Domain.Contexts;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Products;

namespace POS.Api.Services;

public sealed class PosCartService(
    AppDbContext db,
    IInventarioServiceWrapper inventario,
    PosSalesService salesService,
    ILogger<PosCartService> logger)
{
    private static readonly TimeSpan CartTtl = TimeSpan.FromHours(24);

    public async Task<Result<PosCartResponse>> CreateAsync(
        CreatePosCartRequest request,
        CancellationToken ct)
    {
        if (!PosServiceHelpers.TryResolveTenantId(request.Metadata, out var tenantId))
            return Result<PosCartResponse>.Failure("Tenant ID is required", 400);

        var idempotencyKey = PosServiceHelpers.NormalizeOptional(request.IdempotencyKey) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var replay = await LoadCartByIdempotencyAsync(tenantId, idempotencyKey, ct);
            if (replay is not null)
                return Result<PosCartResponse>.Success(PosServiceHelpers.ToCartResponse(replay), "POS cart replayed");
        }

        var register = await LoadRegisterAsync(tenantId, request.RegisterId, ct);
        if (register is null)
            return Result<PosCartResponse>.NotFound("POS register was not found");

        var now = DateTime.UtcNow;
        var cart = new PosCart
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CartNumber = string.Empty,
            RegisterId = register.Id,
            CashierCredentialId = request.CashierCredentialId,
            CustomerCredentialId = request.CustomerCredentialId,
            CustomerLabel = PosServiceHelpers.NormalizeOptional(request.CustomerLabel),
            Notes = PosServiceHelpers.NormalizeOptional(request.Notes),
            WarehouseId = request.WarehouseId ?? register.DefaultWarehouseId,
            LocationId = request.LocationId ?? register.DefaultLocationId,
            CurrencyId = request.CurrencyId ?? register.CurrencyId,
            WalletTypeId = request.WalletTypeId ?? register.WalletTypeId,
            Status = request.Suspend ? PosCartStatus.Suspended : PosCartStatus.Open,
            DiscountAmount = request.DiscountAmount,
            TaxAmount = request.TaxAmount,
            IdempotencyKey = idempotencyKey,
            SuspendedAt = request.Suspend ? now : null,
            ExpiresAt = now.Add(CartTtl),
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid(),
            IsEnabled = true
        };
        cart.CartNumber = PosServiceHelpers.NewCartNumber(now, cart.Id);

        var lineResult = await BuildCartLinesAsync(
            cart.Id,
            tenantId,
            request.Metadata,
            register,
            cart.WarehouseId,
            cart.LocationId,
            request.Lines,
            ct);

        if (!lineResult.IsSuccess)
            return Result<PosCartResponse>.Failure(lineResult.Message!, lineResult.StatusCode);

        cart.Lines = lineResult.Data!;
        var totalsResult = ApplyTotals(cart);
        if (!totalsResult.IsSuccess)
            return Result<PosCartResponse>.Failure(totalsResult.Message!, totalsResult.StatusCode);

        db.Set<PosCart>().Add(cart);
        await db.SaveChangesAsync(ct);

        return Result<PosCartResponse>.Success(
            PosServiceHelpers.ToCartResponse(cart),
            request.Suspend ? "POS cart suspended" : "POS cart created");
    }

    public async Task<Result<PosCartResponse>> UpdateAsync(
        UpdatePosCartRequest request,
        CancellationToken ct)
    {
        if (!PosServiceHelpers.TryResolveTenantId(request.Metadata, out var tenantId))
            return Result<PosCartResponse>.Failure("Tenant ID is required", 400);

        await ExpireDueCartsAsync(tenantId, ct);
        var cart = await LoadCartAsync(tenantId, request.Id, tracking: true, ct);
        if (cart is null)
            return Result<PosCartResponse>.NotFound("POS cart was not found");

        var stateResult = EnsureEditable(cart);
        if (!stateResult.IsSuccess)
            return Result<PosCartResponse>.Failure(stateResult.Message!, stateResult.StatusCode);

        var concurrencyResult = EnsureExpectedConcurrency(cart, request.ExpectedConcurrencyStamp);
        if (!concurrencyResult.IsSuccess)
            return Result<PosCartResponse>.Failure(concurrencyResult.Message!, concurrencyResult.StatusCode);

        var register = await LoadRegisterAsync(tenantId, cart.RegisterId, ct);
        if (register is null)
            return Result<PosCartResponse>.NotFound("POS register was not found");

        cart.CustomerCredentialId = request.CustomerCredentialId;
        cart.CustomerLabel = PosServiceHelpers.NormalizeOptional(request.CustomerLabel);
        cart.Notes = PosServiceHelpers.NormalizeOptional(request.Notes);
        cart.WarehouseId = request.WarehouseId ?? cart.WarehouseId;
        cart.LocationId = request.LocationId ?? cart.LocationId;
        cart.CurrencyId = request.CurrencyId ?? cart.CurrencyId;
        cart.WalletTypeId = request.WalletTypeId ?? cart.WalletTypeId;
        cart.DiscountAmount = request.DiscountAmount;
        cart.TaxAmount = request.TaxAmount;
        cart.ModifiedAt = DateTime.UtcNow;
        cart.ConcurrencyStamp = Guid.NewGuid();

        var lineResult = await BuildCartLinesAsync(
            cart.Id,
            tenantId,
            request.Metadata,
            register,
            cart.WarehouseId,
            cart.LocationId,
            request.Lines,
            ct);

        if (!lineResult.IsSuccess)
            return Result<PosCartResponse>.Failure(lineResult.Message!, lineResult.StatusCode);

        var existingLines = cart.Lines.ToList();
        db.Set<PosCartLine>().RemoveRange(existingLines);
        cart.Lines.Clear();
        foreach (var line in lineResult.Data!)
            cart.Lines.Add(line);

        var totalsResult = ApplyTotals(cart);
        if (!totalsResult.IsSuccess)
            return Result<PosCartResponse>.Failure(totalsResult.Message!, totalsResult.StatusCode);

        await db.SaveChangesAsync(ct);
        return Result<PosCartResponse>.Success(PosServiceHelpers.ToCartResponse(cart), "POS cart updated");
    }

    public async Task<Result<PosCartResponse>> GetAsync(
        GetPosCartRequest request,
        CancellationToken ct)
    {
        if (!PosServiceHelpers.TryResolveTenantId(request.Metadata, out var tenantId))
            return Result<PosCartResponse>.Failure("Tenant ID is required", 400);

        await ExpireDueCartsAsync(tenantId, ct);
        var cart = await LoadCartAsync(tenantId, request.Id, tracking: false, ct);
        if (cart is null)
            return Result<PosCartResponse>.NotFound("POS cart was not found");

        var warnings = await BuildCatalogWarningsAsync(cart, request.Metadata);
        return Result<PosCartResponse>.Success(PosServiceHelpers.ToCartResponse(cart, warnings));
    }

    public async Task<Result<List<PosCartSummaryResponse>>> SearchAsync(
        SearchPosCartsRequest request,
        CancellationToken ct)
    {
        if (!PosServiceHelpers.TryResolveTenantId(request.Metadata, out var tenantId))
            return Result<List<PosCartSummaryResponse>>.Failure("Tenant ID is required", 400);

        await ExpireDueCartsAsync(tenantId, ct);
        var (page, pageSize) = PosServiceHelpers.NormalizePage(request.Page, request.PageSize);
        IQueryable<PosCart> query = db.Set<PosCart>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted);

        if (request.RegisterId.HasValue)
            query = query.Where(item => item.RegisterId == request.RegisterId.Value);

        if (request.CashierCredentialId.HasValue)
            query = query.Where(item => item.CashierCredentialId == request.CashierCredentialId.Value);

        if (request.Status.HasValue)
            query = query.Where(item => item.Status == request.Status.Value);
        else if (!request.IncludeExpired)
            query = query.Where(item => item.Status != PosCartStatus.Expired);

        if (request.From.HasValue)
            query = query.Where(item => item.CreatedAt >= request.From.Value);

        if (request.To.HasValue)
            query = query.Where(item => item.CreatedAt <= request.To.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item =>
                item.CartNumber.Contains(search) ||
                (item.CustomerLabel != null && item.CustomerLabel.Contains(search)) ||
                (item.Notes != null && item.Notes.Contains(search)));
        }

        var items = await query
            .OrderByDescending(item => item.SuspendedAt ?? item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Result<List<PosCartSummaryResponse>>.Success(
            items.Select(PosServiceHelpers.ToCartSummaryResponse).ToList());
    }

    public async Task<Result<PosCartResponse>> SuspendAsync(
        SuspendPosCartRequest request,
        CancellationToken ct)
    {
        if (!PosServiceHelpers.TryResolveTenantId(request.Metadata, out var tenantId))
            return Result<PosCartResponse>.Failure("Tenant ID is required", 400);

        await ExpireDueCartsAsync(tenantId, ct);
        var cart = await LoadCartAsync(tenantId, request.CartId, tracking: true, ct);
        if (cart is null)
            return Result<PosCartResponse>.NotFound("POS cart was not found");

        var stateResult = EnsureEditable(cart);
        if (!stateResult.IsSuccess)
            return Result<PosCartResponse>.Failure(stateResult.Message!, stateResult.StatusCode);

        var concurrencyResult = EnsureExpectedConcurrency(cart, request.ExpectedConcurrencyStamp);
        if (!concurrencyResult.IsSuccess)
            return Result<PosCartResponse>.Failure(concurrencyResult.Message!, concurrencyResult.StatusCode);

        var now = DateTime.UtcNow;
        cart.Status = PosCartStatus.Suspended;
        cart.SuspendedAt = now;
        cart.ExpiresAt ??= now.Add(CartTtl);
        cart.ModifiedAt = now;
        cart.ConcurrencyStamp = Guid.NewGuid();

        await db.SaveChangesAsync(ct);
        return Result<PosCartResponse>.Success(PosServiceHelpers.ToCartResponse(cart), "POS cart suspended");
    }

    public async Task<Result<PosCartResponse>> ResumeAsync(
        ResumePosCartRequest request,
        CancellationToken ct)
    {
        if (!PosServiceHelpers.TryResolveTenantId(request.Metadata, out var tenantId))
            return Result<PosCartResponse>.Failure("Tenant ID is required", 400);

        await ExpireDueCartsAsync(tenantId, ct);
        var cart = await LoadCartAsync(tenantId, request.CartId, tracking: true, ct);
        if (cart is null)
            return Result<PosCartResponse>.NotFound("POS cart was not found");

        var stateResult = EnsureEditable(cart);
        if (!stateResult.IsSuccess)
            return Result<PosCartResponse>.Failure(stateResult.Message!, stateResult.StatusCode);

        var concurrencyResult = EnsureExpectedConcurrency(cart, request.ExpectedConcurrencyStamp);
        if (!concurrencyResult.IsSuccess)
            return Result<PosCartResponse>.Failure(concurrencyResult.Message!, concurrencyResult.StatusCode);

        var now = DateTime.UtcNow;
        cart.Status = PosCartStatus.Open;
        cart.ResumedAt = now;
        cart.ModifiedAt = now;
        cart.ConcurrencyStamp = Guid.NewGuid();

        await db.SaveChangesAsync(ct);
        var warnings = await BuildCatalogWarningsAsync(cart, request.Metadata);
        return Result<PosCartResponse>.Success(PosServiceHelpers.ToCartResponse(cart, warnings), "POS cart resumed");
    }

    public async Task<Result<PosCartResponse>> CancelAsync(
        CancelPosCartRequest request,
        CancellationToken ct)
    {
        if (!PosServiceHelpers.TryResolveTenantId(request.Metadata, out var tenantId))
            return Result<PosCartResponse>.Failure("Tenant ID is required", 400);

        await ExpireDueCartsAsync(tenantId, ct);
        var cart = await LoadCartAsync(tenantId, request.CartId, tracking: true, ct);
        if (cart is null)
            return Result<PosCartResponse>.NotFound("POS cart was not found");

        var stateResult = EnsureEditable(cart);
        if (!stateResult.IsSuccess)
            return Result<PosCartResponse>.Failure(stateResult.Message!, stateResult.StatusCode);

        var concurrencyResult = EnsureExpectedConcurrency(cart, request.ExpectedConcurrencyStamp);
        if (!concurrencyResult.IsSuccess)
            return Result<PosCartResponse>.Failure(concurrencyResult.Message!, concurrencyResult.StatusCode);

        var now = DateTime.UtcNow;
        cart.Status = PosCartStatus.Cancelled;
        cart.CancelledAt = now;
        cart.CancelReason = PosServiceHelpers.NormalizeOptional(request.Reason);
        cart.ModifiedAt = now;
        cart.ConcurrencyStamp = Guid.NewGuid();

        await db.SaveChangesAsync(ct);
        return Result<PosCartResponse>.Success(PosServiceHelpers.ToCartResponse(cart), "POS cart cancelled");
    }

    public async Task<Result<PosSaleReceiptResponse>> CheckoutAsync(
        CheckoutPosCartRequest request,
        CancellationToken ct)
    {
        if (!PosServiceHelpers.TryResolveTenantId(request.Metadata, out var tenantId))
            return Result<PosSaleReceiptResponse>.Failure("Tenant ID is required", 400);

        await ExpireDueCartsAsync(tenantId, ct);
        var cart = await LoadCartAsync(tenantId, request.CartId, tracking: true, ct);
        if (cart is null)
            return Result<PosSaleReceiptResponse>.NotFound("POS cart was not found");

        if (cart.Status == PosCartStatus.Converted)
            return await GetConvertedSaleReceiptAsync(cart, request.Metadata, ct);

        var stateResult = EnsureEditable(cart);
        if (!stateResult.IsSuccess)
            return Result<PosSaleReceiptResponse>.Failure(stateResult.Message!, stateResult.StatusCode);

        var concurrencyResult = EnsureExpectedConcurrency(cart, request.ExpectedConcurrencyStamp);
        if (!concurrencyResult.IsSuccess)
            return Result<PosSaleReceiptResponse>.Failure(concurrencyResult.Message!, concurrencyResult.StatusCode);

        if (cart.Lines.Count == 0)
            return Result<PosSaleReceiptResponse>.Failure("At least one cart line is required", 400);

        var priceResult = await ValidateCurrentPricesAsync(cart, request.Metadata);
        if (!priceResult.IsSuccess)
            return Result<PosSaleReceiptResponse>.Failure(priceResult.Message!, priceResult.StatusCode);

        var saleRequest = BuildCheckoutRequest(cart, request);
        var saleResult = await salesService.CheckoutAsync(saleRequest, ct);
        if (saleResult.IsSuccess && saleResult.Data is not null)
        {
            cart.Status = PosCartStatus.Converted;
            cart.ConvertedSaleId = saleResult.Data.Id;
            cart.ModifiedAt = DateTime.UtcNow;
            cart.ConcurrencyStamp = Guid.NewGuid();
            await db.SaveChangesAsync(ct);
        }

        return saleResult;
    }

    private async Task<Result<List<PosCartLine>>> BuildCartLinesAsync(
        Guid cartId,
        Guid tenantId,
        RequestMetadata metadata,
        PosRegister register,
        Guid defaultWarehouseId,
        Guid defaultLocationId,
        IReadOnlyCollection<PosCartLineRequest> requestLines,
        CancellationToken ct)
    {
        var lines = new List<PosCartLine>();
        var lineNumber = 1;

        foreach (var requestLine in requestLines)
        {
            var productResponse = await inventario.GetSellableProduct(new GetSellableProductRequest
            {
                ProductId = requestLine.ProductId,
                Metadata = metadata
            });

            if (!productResponse.IsSuccess || productResponse.Response is null)
                return Result<List<PosCartLine>>.Failure(
                    productResponse.Message ?? "Inventario product lookup failed",
                    (int)productResponse.HttpStatusCode);

            var product = productResponse.Response;
            var variation = requestLine.ProductVariationId.HasValue
                ? product.Variations.FirstOrDefault(item => item.ProductVariationId == requestLine.ProductVariationId.Value)
                : null;

            if (requestLine.ProductVariationId.HasValue && variation is null)
                return Result<List<PosCartLine>>.NotFound("Product variation was not found");

            var currentPrice = variation?.Price ?? product.Price;
            if (requestLine.ExpectedUnitPrice.HasValue && currentPrice != requestLine.ExpectedUnitPrice.Value)
                return Result<List<PosCartLine>>.Conflict("Catalog price changed before cart save");

            var expectedPrice = requestLine.ExpectedUnitPrice ?? currentPrice;
            var lineTotal = requestLine.Quantity * currentPrice - requestLine.DiscountAmount + requestLine.TaxAmount;
            if (lineTotal < 0)
                return Result<List<PosCartLine>>.Failure("Line total cannot be negative", 400);

            lines.Add(new PosCartLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CartId = cartId,
                LineNumber = lineNumber++,
                ProductId = requestLine.ProductId,
                ProductVariationId = requestLine.ProductVariationId,
                ProductName = product.Name,
                VariantName = variation?.VariantName,
                SKU = product.SKU,
                Quantity = requestLine.Quantity,
                UnitPrice = currentPrice,
                ExpectedUnitPrice = expectedPrice,
                DiscountAmount = requestLine.DiscountAmount,
                TaxAmount = requestLine.TaxAmount,
                LineTotal = lineTotal,
                WarehouseId = requestLine.WarehouseId ?? defaultWarehouseId,
                LocationId = requestLine.LocationId ?? defaultLocationId,
                LotId = requestLine.LotId,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid(),
                IsEnabled = true
            });
        }

        return Result<List<PosCartLine>>.Success(lines);
    }

    private async Task<IReadOnlyCollection<string>> BuildCatalogWarningsAsync(
        PosCart cart,
        RequestMetadata metadata)
    {
        var warnings = new List<string>();

        foreach (var line in cart.Lines.OrderBy(item => item.LineNumber))
        {
            var productResponse = await inventario.GetSellableProduct(new GetSellableProductRequest
            {
                ProductId = line.ProductId,
                Metadata = metadata
            });

            if (!productResponse.IsSuccess || productResponse.Response is null)
            {
                warnings.Add($"{line.ProductName}: product is no longer available for sale.");
                continue;
            }

            var product = productResponse.Response;
            if (!product.IsAvailable)
                warnings.Add($"{line.ProductName}: product is currently unavailable.");

            var variation = line.ProductVariationId.HasValue
                ? product.Variations.FirstOrDefault(item => item.ProductVariationId == line.ProductVariationId.Value)
                : null;

            if (line.ProductVariationId.HasValue && variation is null)
            {
                warnings.Add($"{line.ProductName}: selected variation is no longer available.");
                continue;
            }

            var currentPrice = variation?.Price ?? product.Price;
            if (currentPrice != line.ExpectedUnitPrice)
                warnings.Add($"{line.ProductName}: price changed from {line.ExpectedUnitPrice:N2} to {currentPrice:N2}.");
        }

        return warnings;
    }

    private async Task<Result> ValidateCurrentPricesAsync(
        PosCart cart,
        RequestMetadata metadata)
    {
        var warnings = await BuildCatalogWarningsAsync(cart, metadata);
        if (warnings.Count == 0)
            return Result.Success();

        logger.LogWarning(
            "POS cart {CartId} checkout blocked by catalog warnings: {Warnings}",
            cart.Id,
            string.Join("; ", warnings));
        return Result.Conflict(string.Join(" ", warnings));
    }

    private CheckoutPosSaleRequest BuildCheckoutRequest(
        PosCart cart,
        CheckoutPosCartRequest request)
    {
        var customerCredentialId = request.Payment.CustomerCredentialId ?? cart.CustomerCredentialId;
        return new CheckoutPosSaleRequest
        {
            Metadata = request.Metadata,
            RegisterId = cart.RegisterId,
            CashierCredentialId = cart.CashierCredentialId,
            CustomerCredentialId = customerCredentialId,
            WarehouseId = cart.WarehouseId,
            LocationId = cart.LocationId,
            CurrencyId = cart.CurrencyId,
            WalletTypeId = cart.WalletTypeId,
            DiscountAmount = cart.DiscountAmount,
            TaxAmount = cart.TaxAmount,
            IdempotencyKey = PosServiceHelpers.NormalizeOptional(request.IdempotencyKey) ?? $"POS.CartCheckout.{cart.Id:N}",
            Lines = cart.Lines
                .OrderBy(line => line.LineNumber)
                .Select(line => new CheckoutPosSaleLineRequest
                {
                    ProductId = line.ProductId,
                    ProductVariationId = line.ProductVariationId,
                    Quantity = line.Quantity,
                    ExpectedUnitPrice = line.ExpectedUnitPrice,
                    DiscountAmount = line.DiscountAmount,
                    TaxAmount = line.TaxAmount,
                    WarehouseId = line.WarehouseId,
                    LocationId = line.LocationId,
                    LotId = line.LotId
                })
                .ToList(),
            Payment = new CheckoutPosPaymentRequest
            {
                Method = request.Payment.Method,
                Amount = cart.TotalAmount,
                CustomerCredentialId = customerCredentialId
            }
        };
    }

    private async Task<Result<PosSaleReceiptResponse>> GetConvertedSaleReceiptAsync(
        PosCart cart,
        RequestMetadata metadata,
        CancellationToken ct)
    {
        if (cart.ConvertedSaleId is null)
            return Result<PosSaleReceiptResponse>.Conflict("POS cart is converted but has no sale reference");

        return await salesService.GetAsync(new GetPosSaleRequest
        {
            Id = cart.ConvertedSaleId.Value,
            Metadata = metadata
        }, ct);
    }

    private async Task<PosRegister?> LoadRegisterAsync(
        Guid tenantId,
        Guid registerId,
        CancellationToken ct) =>
        await db.Set<PosRegister>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                item.Id == registerId &&
                item.IsEnabled &&
                !item.IsDeleted,
                ct);

    private async Task<PosCart?> LoadCartAsync(
        Guid tenantId,
        Guid cartId,
        bool tracking,
        CancellationToken ct)
    {
        var query = db.Set<PosCart>()
            .Include(item => item.Lines)
            .Where(item => item.TenantId == tenantId && item.Id == cartId && !item.IsDeleted);

        if (!tracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(ct);
    }

    private async Task<PosCart?> LoadCartByIdempotencyAsync(
        Guid tenantId,
        string idempotencyKey,
        CancellationToken ct) =>
        await db.Set<PosCart>()
            .AsNoTracking()
            .Include(item => item.Lines)
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                item.IdempotencyKey == idempotencyKey &&
                !item.IsDeleted,
                ct);

    private async Task ExpireDueCartsAsync(
        Guid tenantId,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var stamp = Guid.NewGuid();
        await db.Set<PosCart>()
            .Where(item =>
                item.TenantId == tenantId &&
                !item.IsDeleted &&
                (item.Status == PosCartStatus.Open || item.Status == PosCartStatus.Suspended) &&
                item.ExpiresAt.HasValue &&
                item.ExpiresAt.Value <= now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, PosCartStatus.Expired)
                .SetProperty(item => item.ModifiedAt, now)
                .SetProperty(item => item.ConcurrencyStamp, stamp),
                ct);
    }

    private static Result ApplyTotals(PosCart cart)
    {
        cart.SubtotalAmount = cart.Lines.Sum(line => line.Quantity * line.UnitPrice);
        cart.TotalAmount = cart.SubtotalAmount - cart.DiscountAmount + cart.TaxAmount;

        return cart.TotalAmount < 0
            ? Result.Failure("Cart total cannot be negative", 400)
            : Result.Success();
    }

    private static Result EnsureEditable(PosCart cart) =>
        cart.Status is PosCartStatus.Open or PosCartStatus.Suspended
            ? Result.Success()
            : Result.Conflict("Only open or suspended POS carts can be changed");

    private static Result EnsureExpectedConcurrency(
        PosCart cart,
        Guid? expectedConcurrencyStamp) =>
        expectedConcurrencyStamp.HasValue && expectedConcurrencyStamp.Value != cart.ConcurrencyStamp
            ? Result.Conflict("POS cart was changed by another operation")
            : Result.Success();
}
