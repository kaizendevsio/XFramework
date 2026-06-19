using System.Security.Claims;
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

        if (request.MovementType == InventoryMovementType.Transfer)
            return await PostTransferAsync(tenantId, request, saveChanges, ct);

        var product = await GetProduct(tenantId, request.ProductId, ct);
        if (product is null)
            return Result<StockPostingResponse>.NotFound("Product not found.");

        var balanceResult = await GetOrCreateBalance(tenantId, request.ProductId, request.WarehouseId, request.LocationId, ct);
        if (!balanceResult.IsSuccess)
            return Result<StockPostingResponse>.Failure(balanceResult.Message!, balanceResult.StatusCode);

        var balance = balanceResult.Data!;
        var before = balance.OnHandQuantity;
        var delta = GetOnHandDelta(request.MovementType, request.Quantity);
        var reservedDelta = GetReservedDelta(request.MovementType, request.Quantity);
        var afterOnHand = before + delta;
        var afterReserved = balance.ReservedQuantity + reservedDelta;

        if (!request.AllowNegativeStock && (afterOnHand < 0 || afterReserved < 0 || afterOnHand - afterReserved < 0))
            return Result<StockPostingResponse>.Failure("Stock movement would create a negative stock position.", 409);

        ApplyBalance(balance, afterOnHand, afterReserved);
        AddMovement(tenantId, request, balance.Id, delta == 0 ? reservedDelta : delta, before, afterOnHand);
        await UpdateProductSnapshot(tenantId, product, request.ProductId, delta, ct);

        if (saveChanges)
        {
            var saveResult = await dataContext.SaveChangesAsync(ct);
            if (!saveResult.IsSuccess)
                return Result<StockPostingResponse>.Failure(saveResult.Message ?? "Stock posting failed.", saveResult.StatusCode);
        }

        return Result<StockPostingResponse>.Success(new StockPostingResponse(
            balance.Id,
            request.ProductId,
            balance.WarehouseId,
            balance.LocationId,
            balance.OnHandQuantity,
            balance.ReservedQuantity,
            balance.AvailableQuantity));
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

        var movements = await query
            .OrderByDescending(x => x.MovementDate)
            .Take(500)
            .ToListAsync(ct);

        return Result<List<InventoryMovement>>.Success(movements);
    }

    private async Task<Result<StockPostingResponse>> PostTransferAsync(
        Guid tenantId,
        PostStockMovementRequest request,
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

        var sourceResult = await GetOrCreateBalance(tenantId, request.ProductId, request.WarehouseId, request.LocationId, ct);
        if (!sourceResult.IsSuccess)
            return Result<StockPostingResponse>.Failure(sourceResult.Message!, sourceResult.StatusCode);

        var destinationResult = await GetOrCreateBalance(tenantId, request.ProductId, destinationWarehouseId, destinationLocationId, ct);
        if (!destinationResult.IsSuccess)
            return Result<StockPostingResponse>.Failure(destinationResult.Message!, destinationResult.StatusCode);

        var source = sourceResult.Data!;
        var destination = destinationResult.Data!;
        var sourceAfter = source.OnHandQuantity - request.Quantity;
        if (!request.AllowNegativeStock && sourceAfter < source.ReservedQuantity)
            return Result<StockPostingResponse>.Failure("Transfer would create a negative source stock position.", 409);

        var now = DateTime.UtcNow;
        var sourceBefore = source.OnHandQuantity;
        var destinationBefore = destination.OnHandQuantity;
        ApplyBalance(source, sourceAfter, source.ReservedQuantity, now);
        ApplyBalance(destination, destination.OnHandQuantity + request.Quantity, destination.ReservedQuantity, now);

        AddMovement(tenantId, request, source.Id, -request.Quantity, sourceBefore, source.OnHandQuantity);
        AddMovement(tenantId, request with
        {
            WarehouseId = destinationWarehouseId,
            LocationId = destinationLocationId
        }, destination.Id, request.Quantity, destinationBefore, destination.OnHandQuantity);

        await UpdateProductSnapshot(tenantId, product, request.ProductId, 0, ct);

        if (saveChanges)
        {
            var saveResult = await dataContext.SaveChangesAsync(ct);
            if (!saveResult.IsSuccess)
                return Result<StockPostingResponse>.Failure(saveResult.Message ?? "Stock transfer failed.", saveResult.StatusCode);
        }

        return Result<StockPostingResponse>.Success(new StockPostingResponse(
            destination.Id,
            request.ProductId,
            destination.WarehouseId,
            destination.LocationId,
            destination.OnHandQuantity,
            destination.ReservedQuantity,
            destination.AvailableQuantity));
    }

    private async Task<Product?> GetProduct(Guid tenantId, Guid productId, CancellationToken ct) =>
        await dataContext.Query<Product>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.Id == productId && !x.IsDeleted)
            .FirstOrDefaultAsync(ct);

    private async Task<Result<StockBalance>> GetOrCreateBalance(
        Guid tenantId,
        Guid productId,
        Guid warehouseId,
        Guid locationId,
        CancellationToken ct)
    {
        var warehouseExists = await dataContext.Query<Warehouse>()
            .IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenantId && x.Id == warehouseId && !x.IsDeleted, ct);
        if (!warehouseExists)
            return Result<StockBalance>.NotFound("Warehouse not found.");

        var locationExists = await dataContext.Query<InventoryLocation>()
            .IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenantId && x.Id == locationId && x.WarehouseId == warehouseId && !x.IsDeleted, ct);
        if (!locationExists)
            return Result<StockBalance>.NotFound("Location not found.");

        var balance = await dataContext.Query<StockBalance>()
            .IgnoreQueryFilters()
            .Where(x =>
                x.TenantId == tenantId &&
                x.ProductId == productId &&
                x.WarehouseId == warehouseId &&
                x.LocationId == locationId &&
                !x.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (balance is not null)
            return Result<StockBalance>.Success(balance);

        balance = new StockBalance
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            WarehouseId = warehouseId,
            LocationId = locationId,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };
        dataContext.Add(balance);
        return Result<StockBalance>.Success(balance);
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
        decimal quantityAfter)
    {
        dataContext.Add(new InventoryMovement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = request.ProductId,
            WarehouseId = request.WarehouseId,
            LocationId = request.LocationId,
            StockBalanceId = stockBalanceId,
            MovementType = request.MovementType,
            QuantityDelta = quantityDelta,
            QuantityBefore = quantityBefore,
            QuantityAfter = quantityAfter,
            MovementDate = DateTime.UtcNow,
            UnitOfMeasure = request.UnitOfMeasure,
            ReferenceType = request.ReferenceType,
            ReferenceId = request.ReferenceId,
            Reason = request.Reason,
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
}
