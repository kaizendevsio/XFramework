using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;
using XFramework.Inventario.Domain.Shared.Contracts.Responses;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Api.Services;

public sealed class StockPostingService(
    IDataContext dataContext,
    IHttpContextAccessor httpContextAccessor)
{
    private static readonly JsonSerializerOptions HashJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<StockPostingResponse>> PostAsync(
        PostStockMovementRequest request,
        CancellationToken ct = default) =>
        await PostCoreAsync(request, saveChanges: true, ct);

    internal async Task<Result<StockPostingResponse>> StageAsync(
        PostStockMovementRequest request,
        CancellationToken ct = default) =>
        await PostCoreAsync(request, saveChanges: false, ct);

    private async Task<Result<StockPostingResponse>> PostCoreAsync(
        PostStockMovementRequest request,
        bool saveChanges,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<StockPostingResponse>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var tenantId = tenantResult.Data;
        if (request.Quantity == 0)
            return Result<StockPostingResponse>.Failure("Quantity must not be zero.", 400);

        if (request.MovementType != InventoryMovementType.Adjustment && request.Quantity < 0)
            return Result<StockPostingResponse>.Failure("Quantity must be positive for this movement type.", 400);

        var requestHash = ComputeRequestHash(tenantId, request);
        var replay = await FindIdempotentReplayAsync(tenantId, request, requestHash, ct);
        if (replay is not null)
            return replay;

        if (request.MovementType == InventoryMovementType.Transfer)
            return await PostTransferAsync(tenantId, request, requestHash, saveChanges, ct);

        var product = await GetProduct(tenantId, request.ProductId, ct);
        if (product is null)
            return Result<StockPostingResponse>.NotFound("Product not found.");

        var lotResult = await ValidateLotAsync(tenantId, request.ProductId, request.LotId, ct);
        if (!lotResult.IsSuccess)
            return Result<StockPostingResponse>.Failure(lotResult.Message!, lotResult.StatusCode);

        var balanceResult = await GetOrCreateBalance(
            tenantId,
            request.ProductId,
            request.WarehouseId,
            request.LocationId,
            request.LotId,
            ct);
        if (!balanceResult.IsSuccess)
            return Result<StockPostingResponse>.Failure(balanceResult.Message!, balanceResult.StatusCode);

        var balanceState = balanceResult.Data!;
        var balance = balanceState.Balance;
        var before = balance.OnHandQuantity;
        var delta = GetOnHandDelta(request.MovementType, request.Quantity);
        var reservedDelta = GetReservedDelta(request.MovementType, request.Quantity);
        var afterOnHand = before + delta;
        var afterReserved = balance.ReservedQuantity + reservedDelta;

        if (!request.AllowNegativeStock && (afterOnHand < 0 || afterReserved < 0 || afterOnHand - afterReserved < 0))
            return Result<StockPostingResponse>.Failure("Stock movement would create a negative stock position.", 409);

        if (!balanceState.IsNew)
            dataContext.Update(balance);

        ApplyBalance(balance, afterOnHand, afterReserved);
        AddMovement(
            tenantId,
            request,
            balance.Id,
            delta == 0 ? reservedDelta : delta,
            before,
            afterOnHand,
            requestHash,
            includeIdempotency: true);
        await UpdateProductSnapshot(tenantId, product, request.ProductId, delta, ct);

        if (saveChanges)
        {
            var saveResult = await dataContext.SaveChangesAsync(ct);
            if (!saveResult.IsSuccess)
                return Result<StockPostingResponse>.Failure(saveResult.Message ?? "Stock posting failed.", saveResult.StatusCode);
        }

        return Result<StockPostingResponse>.Success(CreateResponse(
            balance.Id,
            request.ProductId,
            balance.WarehouseId,
            balance.LocationId,
            balance.OnHandQuantity,
            balance.ReservedQuantity,
            balance.AvailableQuantity,
            request.LotId,
            NormalizeIdempotencyKey(request.IdempotencyKey),
            isReplay: false));
    }

    public async Task<Result<List<StockBalance>>> GetBalancesAsync(
        GetStockBalancesRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<List<StockBalance>>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var tenantId = tenantResult.Data;
        var query = dataContext.Query<StockBalance>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted);

        if (request.ProductId is { } id)
            query = query.Where(x => x.ProductId == id);

        if (request.WarehouseId is { } warehouseId)
            query = query.Where(x => x.WarehouseId == warehouseId);

        if (request.LocationId is { } locationId)
            query = query.Where(x => x.LocationId == locationId);

        if (request.LotId is { } lotId)
            query = query.Where(x => x.LotId == lotId);

        var balances = await query
            .OrderBy(x => x.ProductId)
            .Take(500)
            .ToListAsync(ct);

        return Result<List<StockBalance>>.Success(balances);
    }

    public async Task<Result<List<InventoryMovement>>> GetMovementsAsync(
        GetInventoryMovementsRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<List<InventoryMovement>>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var tenantId = tenantResult.Data;
        var query = dataContext.Query<InventoryMovement>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted);

        if (request.ProductId is { } id)
            query = query.Where(x => x.ProductId == id);

        if (request.WarehouseId is { } warehouseId)
            query = query.Where(x => x.WarehouseId == warehouseId);

        if (request.LocationId is { } locationId)
            query = query.Where(x => x.LocationId == locationId);

        if (request.LotId is { } lotId)
            query = query.Where(x => x.LotId == lotId);

        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey);
        if (idempotencyKey is not null)
            query = query.Where(x => x.IdempotencyKey == idempotencyKey);

        var movements = await query
            .OrderByDescending(x => x.MovementDate)
            .Take(500)
            .ToListAsync(ct);

        return Result<List<InventoryMovement>>.Success(movements);
    }

    private async Task<Result<StockPostingResponse>> PostTransferAsync(
        Guid tenantId,
        PostStockMovementRequest request,
        string requestHash,
        bool saveChanges,
        CancellationToken ct)
    {
        if (request.DestinationWarehouseId is not { } destinationWarehouseId ||
            request.DestinationLocationId is not { } destinationLocationId)
        {
            return Result<StockPostingResponse>.Failure("Transfers require destination warehouse and location.", 400);
        }

        var product = await GetProduct(tenantId, request.ProductId, ct);
        if (product is null)
            return Result<StockPostingResponse>.NotFound("Product not found.");

        var lotResult = await ValidateLotAsync(tenantId, request.ProductId, request.LotId, ct);
        if (!lotResult.IsSuccess)
            return Result<StockPostingResponse>.Failure(lotResult.Message!, lotResult.StatusCode);

        var sourceResult = await GetOrCreateBalance(
            tenantId,
            request.ProductId,
            request.WarehouseId,
            request.LocationId,
            request.LotId,
            ct);
        if (!sourceResult.IsSuccess)
            return Result<StockPostingResponse>.Failure(sourceResult.Message!, sourceResult.StatusCode);

        var destinationResult = await GetOrCreateBalance(
            tenantId,
            request.ProductId,
            destinationWarehouseId,
            destinationLocationId,
            request.LotId,
            ct);
        if (!destinationResult.IsSuccess)
            return Result<StockPostingResponse>.Failure(destinationResult.Message!, destinationResult.StatusCode);

        var sourceState = sourceResult.Data!;
        var destinationState = destinationResult.Data!;
        var source = sourceState.Balance;
        var destination = destinationState.Balance;
        var sourceAfter = source.OnHandQuantity - request.Quantity;
        if (!request.AllowNegativeStock && sourceAfter < source.ReservedQuantity)
            return Result<StockPostingResponse>.Failure("Transfer would create a negative source stock position.", 409);

        var now = DateTime.UtcNow;
        var sourceBefore = source.OnHandQuantity;
        var destinationBefore = destination.OnHandQuantity;
        if (!sourceState.IsNew)
            dataContext.Update(source);
        if (!destinationState.IsNew)
            dataContext.Update(destination);

        ApplyBalance(source, sourceAfter, source.ReservedQuantity, now);
        ApplyBalance(destination, destination.OnHandQuantity + request.Quantity, destination.ReservedQuantity, now);

        AddMovement(
            tenantId,
            request,
            source.Id,
            -request.Quantity,
            sourceBefore,
            source.OnHandQuantity,
            requestHash,
            includeIdempotency: true);
        AddMovement(tenantId, request with
        {
            WarehouseId = destinationWarehouseId,
            LocationId = destinationLocationId
        }, destination.Id, request.Quantity, destinationBefore, destination.OnHandQuantity, requestHash, includeIdempotency: false);

        await UpdateProductSnapshot(tenantId, product, request.ProductId, 0, ct);

        if (saveChanges)
        {
            var saveResult = await dataContext.SaveChangesAsync(ct);
            if (!saveResult.IsSuccess)
                return Result<StockPostingResponse>.Failure(saveResult.Message ?? "Stock transfer failed.", saveResult.StatusCode);
        }

        return Result<StockPostingResponse>.Success(CreateResponse(
            destination.Id,
            request.ProductId,
            destination.WarehouseId,
            destination.LocationId,
            destination.OnHandQuantity,
            destination.ReservedQuantity,
            destination.AvailableQuantity,
            request.LotId,
            NormalizeIdempotencyKey(request.IdempotencyKey),
            isReplay: false));
    }

    private async Task<Product?> GetProduct(Guid tenantId, Guid productId, CancellationToken ct) =>
        await dataContext.Query<Product>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.Id == productId && !x.IsDeleted)
            .FirstOrDefaultAsync(ct);

    private async Task<Result<InventoryLot?>> ValidateLotAsync(
        Guid tenantId,
        Guid productId,
        Guid? lotId,
        CancellationToken ct)
    {
        if (lotId is null)
            return Result<InventoryLot?>.Success(null);

        var lot = await dataContext.Query<InventoryLot>()
            .IgnoreQueryFilters()
            .Where(x =>
                x.TenantId == tenantId &&
                x.Id == lotId.Value &&
                !x.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (lot is null)
            return Result<InventoryLot?>.NotFound("Lot not found.");

        if (lot.ProductId != productId)
            return Result<InventoryLot?>.Failure("Lot does not belong to the requested product.", 400);

        return Result<InventoryLot?>.Success(lot);
    }

    private async Task<Result<StockBalanceLookup>> GetOrCreateBalance(
        Guid tenantId,
        Guid productId,
        Guid warehouseId,
        Guid locationId,
        Guid? lotId,
        CancellationToken ct)
    {
        var warehouseExists = await dataContext.Query<Warehouse>()
            .IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenantId && x.Id == warehouseId && !x.IsDeleted, ct);
        if (!warehouseExists)
            return Result<StockBalanceLookup>.NotFound("Warehouse not found.");

        var locationExists = await dataContext.Query<InventoryLocation>()
            .IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenantId && x.Id == locationId && x.WarehouseId == warehouseId && !x.IsDeleted, ct);
        if (!locationExists)
            return Result<StockBalanceLookup>.NotFound("Location not found.");

        var balance = await dataContext.Query<StockBalance>()
            .IgnoreQueryFilters()
            .Where(x =>
                x.TenantId == tenantId &&
                x.ProductId == productId &&
                x.WarehouseId == warehouseId &&
                x.LocationId == locationId &&
                x.LotId == lotId &&
                !x.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (balance is not null)
            return Result<StockBalanceLookup>.Success(new StockBalanceLookup(balance, IsNew: false));

        balance = new StockBalance
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            WarehouseId = warehouseId,
            LocationId = locationId,
            LotId = lotId,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };
        dataContext.Add(balance);
        return Result<StockBalanceLookup>.Success(new StockBalanceLookup(balance, IsNew: true));
    }

    private static decimal GetOnHandDelta(InventoryMovementType movementType, decimal quantity) =>
        movementType switch
        {
            InventoryMovementType.OpeningBalance or InventoryMovementType.Receipt or InventoryMovementType.Return => quantity,
            InventoryMovementType.Adjustment => quantity,
            InventoryMovementType.Shipment => -quantity,
            _ => 0
        };

    private static decimal GetReservedDelta(InventoryMovementType movementType, decimal quantity) =>
        movementType switch
        {
            InventoryMovementType.Reservation => quantity,
            InventoryMovementType.Release => -quantity,
            _ => 0
        };

    private static void ApplyBalance(
        StockBalance balance,
        decimal onHandQuantity,
        decimal reservedQuantity,
        DateTime? movementDate = null)
    {
        balance.OnHandQuantity = onHandQuantity;
        balance.ReservedQuantity = reservedQuantity;
        balance.AvailableQuantity = onHandQuantity - reservedQuantity;
        balance.LastMovementAt = movementDate ?? DateTime.UtcNow;
        balance.ModifiedAt = DateTime.UtcNow;
        balance.ConcurrencyStamp = Guid.NewGuid();
    }

    private void AddMovement(
        Guid tenantId,
        PostStockMovementRequest request,
        Guid stockBalanceId,
        decimal quantityDelta,
        decimal quantityBefore,
        decimal quantityAfter,
        string requestHash,
        bool includeIdempotency)
    {
        var idempotencyKey = includeIdempotency
            ? NormalizeIdempotencyKey(request.IdempotencyKey)
            : null;

        dataContext.Add(new InventoryMovement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = request.ProductId,
            WarehouseId = request.WarehouseId,
            LocationId = request.LocationId,
            StockBalanceId = stockBalanceId,
            LotId = request.LotId,
            MovementType = request.MovementType,
            QuantityDelta = quantityDelta,
            QuantityBefore = quantityBefore,
            QuantityAfter = quantityAfter,
            MovementDate = DateTime.UtcNow,
            UnitOfMeasure = request.UnitOfMeasure,
            ReferenceType = request.ReferenceType,
            ReferenceId = request.ReferenceId,
            Reason = request.Reason,
            IdempotencyKey = idempotencyKey,
            RequestHash = idempotencyKey is null ? null : requestHash,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task UpdateProductSnapshot(
        Guid tenantId,
        Product product,
        Guid productId,
        decimal currentDelta,
        CancellationToken ct)
    {
        await Task.CompletedTask;
        product.StockQuantity += (int)Math.Round(currentDelta, MidpointRounding.AwayFromZero);
        product.ModifiedAt = DateTime.UtcNow;
        dataContext.Update(product);
    }

    private Result<Guid> GetCurrentTenantId(RequestBase? request)
    {
        if (request?.Metadata?.TenantId is { } metadataTenantId && metadataTenantId != Guid.Empty)
            return Result<Guid>.Success(metadataTenantId);

        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return Result<Guid>.Unauthorized("Authentication is required for stock operations.");

        var tenantIdClaim = user.FindFirst("tenantId")?.Value
            ?? user.FindFirst("TenantId")?.Value
            ?? user.FindFirst("tid")?.Value;

        if (Guid.TryParse(tenantIdClaim, out var tenantId) && tenantId != Guid.Empty)
            return Result<Guid>.Success(tenantId);

        return Result<Guid>.Forbidden("Authenticated user does not have a valid tenant context.");
    }

    private async Task<Result<StockPostingResponse>?> FindIdempotentReplayAsync(
        Guid tenantId,
        PostStockMovementRequest request,
        string requestHash,
        CancellationToken ct)
    {
        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey);
        if (idempotencyKey is null)
            return null;

        var existing = await dataContext.Query<InventoryMovement>()
            .IgnoreQueryFilters()
            .Where(x =>
                x.TenantId == tenantId &&
                !x.IsDeleted &&
                x.IdempotencyKey == idempotencyKey)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (existing is null)
            return null;

        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return Result<StockPostingResponse>.Conflict(
                "Idempotency key was already used with a different request");
        }

        if (existing.StockBalanceId is not { } stockBalanceId)
            return Result<StockPostingResponse>.Conflict("Processed stock movement is missing a stock balance.");

        var balance = await dataContext.Query<StockBalance>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.Id == stockBalanceId && !x.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (balance is null)
            return Result<StockPostingResponse>.Conflict("Processed stock movement balance was not found.");

        return Result<StockPostingResponse>.Success(CreateResponse(
            balance.Id,
            balance.ProductId,
            balance.WarehouseId,
            balance.LocationId,
            balance.OnHandQuantity,
            balance.ReservedQuantity,
            balance.AvailableQuantity,
            balance.LotId,
            idempotencyKey,
            isReplay: true), "Stock movement already processed.");
    }

    private static string? NormalizeIdempotencyKey(string? idempotencyKey) =>
        string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();

    private static string ComputeRequestHash(Guid tenantId, PostStockMovementRequest request)
    {
        var hashPayload = new
        {
            tenantId,
            request.ProductId,
            request.WarehouseId,
            request.LocationId,
            request.LotId,
            request.DestinationWarehouseId,
            request.DestinationLocationId,
            request.MovementType,
            request.Quantity,
            request.UnitOfMeasure,
            request.ReferenceType,
            request.ReferenceId,
            request.Reason,
            request.AllowNegativeStock
        };

        var json = JsonSerializer.Serialize(hashPayload, HashJsonOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }

    private static StockPostingResponse CreateResponse(
        Guid stockBalanceId,
        Guid productId,
        Guid warehouseId,
        Guid locationId,
        decimal onHandQuantity,
        decimal reservedQuantity,
        decimal availableQuantity,
        Guid? lotId,
        string? idempotencyKey,
        bool isReplay) =>
        new(
            stockBalanceId,
            productId,
            warehouseId,
            locationId,
            onHandQuantity,
            reservedQuantity,
            availableQuantity)
        {
            LotId = lotId,
            IdempotencyKey = idempotencyKey,
            IsIdempotentReplay = isReplay
        };

    private sealed record StockBalanceLookup(StockBalance Balance, bool IsNew);
}
