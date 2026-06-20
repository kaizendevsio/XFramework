using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Api.Services;

public sealed class InventoryAllocationService(
    IDataContext dataContext,
    IHttpContextAccessor httpContextAccessor,
    StockPostingService stockPostingService)
{
    public async Task<Result<List<ReservationAllocation>>> GetAllocationsAsync(
        GetReservationAllocationsRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<List<ReservationAllocation>>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var tenantId = tenantResult.Data;
        var query = dataContext.Query<ReservationAllocation>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted);

        if (request.ReservationId is { } reservationId)
            query = query.Where(x => x.ReservationId == reservationId);

        if (request.ProductId is { } productId)
            query = query.Where(x => x.ProductId == productId);

        if (request.LotId is { } lotId)
            query = query.Where(x => x.LotId == lotId);

        if (request.Status is { } status)
            query = query.Where(x => x.Status == status);

        var allocations = await query
            .OrderByDescending(x => x.ReservedAt)
            .Take(500)
            .ToListAsync(ct);

        return Result<List<ReservationAllocation>>.Success(allocations);
    }

    public async Task<Result<List<ReservationAllocation>>> ReserveAsync(
        Guid tenantId,
        Guid reservationId,
        ReserveInventoryRequest request,
        CancellationToken ct = default)
    {
        if (request.AllowExpiredLotOverride)
        {
            if (string.IsNullOrWhiteSpace(request.ExpiredLotOverrideReason))
                return Result<List<ReservationAllocation>>.Failure("Expired lot override requires a reason.", 400);

            if (!HasExpiredLotOverrideAuthorization())
                return Result<List<ReservationAllocation>>.Forbidden("Expired lot override requires inventory admin authorization.");
        }

        var candidatesResult = await GetFefoCandidatesAsync(tenantId, request, ct);
        if (!candidatesResult.IsSuccess)
            return Result<List<ReservationAllocation>>.Failure(candidatesResult.Message!, candidatesResult.StatusCode);

        var candidates = candidatesResult.Data!;
        if (candidates.Sum(x => x.AvailableQuantity) < request.Quantity)
            return Result<List<ReservationAllocation>>.Conflict("Insufficient allocatable stock for the requested reservation quantity.");

        var now = DateTime.UtcNow;
        var remaining = request.Quantity;
        var allocations = new List<ReservationAllocation>();

        foreach (var candidate in candidates)
        {
            if (remaining <= 0)
                break;

            var quantity = Math.Min(remaining, candidate.AvailableQuantity);
            var stockResult = await stockPostingService.StageAsync(new PostStockMovementRequest
            {
                Metadata = request.Metadata,
                ProductId = request.ProductId,
                WarehouseId = request.WarehouseId,
                LocationId = request.LocationId,
                LotId = candidate.Balance.LotId,
                MovementType = InventoryMovementType.Reservation,
                Quantity = quantity,
                UnitOfMeasure = request.UnitOfMeasure,
                ReferenceType = "reservation",
                ReferenceId = reservationId,
                Reason = request.Reason ?? "Inventory reservation"
            }, ct);

            if (!stockResult.IsSuccess)
                return Result<List<ReservationAllocation>>.Failure(stockResult.Message!, stockResult.StatusCode);

            var allocation = new ReservationAllocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ReservationId = reservationId,
                ProductId = request.ProductId,
                WarehouseId = request.WarehouseId,
                LocationId = request.LocationId,
                StockBalanceId = stockResult.Data!.StockBalanceId,
                LotId = candidate.Balance.LotId,
                Quantity = quantity,
                Status = ReservationAllocationStatus.Reserved,
                ReservedAt = now,
                ExpiredLotOverrideReason = candidate.IsExpiredLot ? NormalizeOptional(request.ExpiredLotOverrideReason) : null,
                IsEnabled = true,
                CreatedAt = now,
                ConcurrencyStamp = Guid.NewGuid()
            };

            dataContext.Add(allocation);
            allocations.Add(allocation);
            remaining -= quantity;
        }

        return Result<List<ReservationAllocation>>.Success(allocations);
    }

    public async Task<Result<List<ReservationAllocation>>> ReleaseAsync(
        Reservation reservation,
        RequestBase request,
        ReservationAllocationStatus terminalStatus,
        string reason,
        CancellationToken ct = default)
    {
        var allocationsResult = await GetReservedAllocationsOrBackfillAsync(reservation, ct);
        if (!allocationsResult.IsSuccess)
            return Result<List<ReservationAllocation>>.Failure(allocationsResult.Message!, allocationsResult.StatusCode);

        var allocations = allocationsResult.Data!;
        var now = DateTime.UtcNow;

        foreach (var allocation in allocations)
        {
            var releaseResult = await stockPostingService.StageAsync(new PostStockMovementRequest
            {
                Metadata = request.Metadata,
                ProductId = allocation.ProductId,
                WarehouseId = allocation.WarehouseId,
                LocationId = allocation.LocationId,
                LotId = allocation.LotId,
                MovementType = InventoryMovementType.Release,
                Quantity = allocation.Quantity,
                ReferenceType = "reservation",
                ReferenceId = reservation.Id,
                Reason = reason
            }, ct);

            if (!releaseResult.IsSuccess)
                return Result<List<ReservationAllocation>>.Failure(releaseResult.Message!, releaseResult.StatusCode);

            CompleteAllocation(allocation, terminalStatus, now);
        }

        return Result<List<ReservationAllocation>>.Success(allocations);
    }

    public async Task<Result<List<ReservationAllocation>>> FulfillAsync(
        Reservation reservation,
        RequestBase request,
        string releaseReason,
        string shipmentReason,
        CancellationToken ct = default)
    {
        var releaseResult = await ReleaseAsync(
            reservation,
            request,
            ReservationAllocationStatus.Fulfilled,
            releaseReason,
            ct);
        if (!releaseResult.IsSuccess)
            return releaseResult;

        foreach (var allocation in releaseResult.Data!)
        {
            var shipmentResult = await stockPostingService.StageAsync(new PostStockMovementRequest
            {
                Metadata = request.Metadata,
                ProductId = allocation.ProductId,
                WarehouseId = allocation.WarehouseId,
                LocationId = allocation.LocationId,
                LotId = allocation.LotId,
                MovementType = InventoryMovementType.Shipment,
                Quantity = allocation.Quantity,
                ReferenceType = "reservation",
                ReferenceId = reservation.Id,
                Reason = shipmentReason
            }, ct);

            if (!shipmentResult.IsSuccess)
                return Result<List<ReservationAllocation>>.Failure(shipmentResult.Message!, shipmentResult.StatusCode);
        }

        return releaseResult;
    }

    private async Task<Result<List<AllocationCandidate>>> GetFefoCandidatesAsync(
        Guid tenantId,
        ReserveInventoryRequest request,
        CancellationToken ct)
    {
        var balances = await dataContext.Query<StockBalance>()
            .IgnoreQueryFilters()
            .Where(x =>
                x.TenantId == tenantId &&
                x.ProductId == request.ProductId &&
                x.WarehouseId == request.WarehouseId &&
                x.LocationId == request.LocationId &&
                !x.IsDeleted &&
                x.AvailableQuantity > 0)
            .ToListAsync(ct);

        if (request.LotId is { } requestedLotId)
            balances = balances.Where(x => x.LotId == requestedLotId).ToList();

        if (balances.Count == 0)
            return Result<List<AllocationCandidate>>.Success([]);

        var lotIds = balances
            .Where(x => x.LotId is not null)
            .Select(x => x.LotId!.Value)
            .Distinct()
            .ToList();

        var lots = lotIds.Count == 0
            ? []
            : await dataContext.Query<InventoryLot>()
                .IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId && lotIds.Contains(x.Id) && !x.IsDeleted)
                .ToListAsync(ct);

        var lotMap = lots.ToDictionary(x => x.Id);
        var now = DateTime.UtcNow;
        var candidates = new List<AllocationCandidate>();

        foreach (var balance in balances)
        {
            InventoryLot? lot = null;
            if (balance.LotId is { } lotId && !lotMap.TryGetValue(lotId, out lot))
                continue;

            if (lot is not null && lot.Status is InventoryLotStatus.Quarantined or InventoryLotStatus.Consumed or InventoryLotStatus.Rejected)
                continue;

            var isExpiredLot = lot is not null && IsExpired(lot, now);
            if (isExpiredLot && !request.AllowExpiredLotOverride)
                continue;

            candidates.Add(new AllocationCandidate(balance, lot, balance.AvailableQuantity, isExpiredLot));
        }

        candidates = candidates
            .OrderBy(x => x.Lot is null ? 1 : 0)
            .ThenBy(x => x.Lot?.ExpiresAt ?? DateTime.MaxValue)
            .ThenBy(x => x.Lot?.ReceivedAt ?? DateTime.MaxValue)
            .ThenBy(x => x.Lot?.LotNumber ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Balance.CreatedAt)
            .ToList();

        return Result<List<AllocationCandidate>>.Success(candidates);
    }

    private async Task<Result<List<ReservationAllocation>>> GetReservedAllocationsOrBackfillAsync(
        Reservation reservation,
        CancellationToken ct)
    {
        var allocations = await dataContext.Query<ReservationAllocation>()
            .IgnoreQueryFilters()
            .Where(x =>
                x.TenantId == reservation.TenantId &&
                x.ReservationId == reservation.Id &&
                x.Status == ReservationAllocationStatus.Reserved &&
                !x.IsDeleted)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

        if (allocations.Count > 0)
            return Result<List<ReservationAllocation>>.Success(allocations);

        if (reservation.StockBalanceId is not { } stockBalanceId)
            return Result<List<ReservationAllocation>>.Failure("Reservation does not have stock allocation details.", 409);

        var balance = await dataContext.Query<StockBalance>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == reservation.TenantId && x.Id == stockBalanceId && !x.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (balance is null)
            return Result<List<ReservationAllocation>>.NotFound("Reservation stock balance not found.");

        var allocation = new ReservationAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = reservation.TenantId,
            ReservationId = reservation.Id,
            ProductId = reservation.ProductId,
            WarehouseId = balance.WarehouseId,
            LocationId = balance.LocationId,
            StockBalanceId = balance.Id,
            LotId = balance.LotId,
            Quantity = reservation.Quantity,
            Status = ReservationAllocationStatus.Reserved,
            ReservedAt = reservation.ReservedAt,
            IsEnabled = true,
            CreatedAt = reservation.ReservedAt == default ? DateTime.UtcNow : reservation.ReservedAt,
            ConcurrencyStamp = Guid.NewGuid()
        };

        dataContext.Add(allocation);
        return Result<List<ReservationAllocation>>.Success([allocation]);
    }

    private void CompleteAllocation(
        ReservationAllocation allocation,
        ReservationAllocationStatus terminalStatus,
        DateTime completedAt)
    {
        dataContext.Update(allocation);

        allocation.Status = terminalStatus;
        allocation.ReleasedAt = completedAt;
        allocation.FulfilledAt = terminalStatus == ReservationAllocationStatus.Fulfilled ? completedAt : null;
        allocation.ModifiedAt = completedAt;
        allocation.ConcurrencyStamp = Guid.NewGuid();
    }

    private bool HasExpiredLotOverrideAuthorization()
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return false;

        return user.Claims.Any(IsOverrideClaim);
    }

    private static bool IsOverrideClaim(Claim claim)
    {
        if (claim.Type is ClaimTypes.Role or "role" or "roles" &&
            (claim.Value.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
             claim.Value.Equals("InventoryAdmin", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (claim.Type is "permission" or "permissions" &&
            claim.Value.Equals("inventario.override_expired_lot", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return claim.Type.Equals("isAdmin", StringComparison.OrdinalIgnoreCase) &&
               bool.TryParse(claim.Value, out var isAdmin) &&
               isAdmin;
    }

    private Result<Guid> GetCurrentTenantId(RequestBase? request)
    {
        if (request?.Metadata?.TenantId is { } metadataTenantId && metadataTenantId != Guid.Empty)
            return Result<Guid>.Success(metadataTenantId);

        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return Result<Guid>.Unauthorized("Authentication is required for allocation operations.");

        var tenantIdClaim = user.FindFirst("tenantId")?.Value
            ?? user.FindFirst("TenantId")?.Value
            ?? user.FindFirst("tid")?.Value;

        if (Guid.TryParse(tenantIdClaim, out var tenantId) && tenantId != Guid.Empty)
            return Result<Guid>.Success(tenantId);

        return Result<Guid>.Forbidden("Authenticated user does not have a valid tenant context.");
    }

    private static bool IsExpired(InventoryLot lot, DateTime now) =>
        lot.Status == InventoryLotStatus.Expired || lot.ExpiresAt is { } expiresAt && expiresAt <= now;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record AllocationCandidate(
        StockBalance Balance,
        InventoryLot? Lot,
        decimal AvailableQuantity,
        bool IsExpiredLot);
}
