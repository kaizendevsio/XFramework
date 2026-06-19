using Microsoft.AspNetCore.Http;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.DataContext;
using XFramework.Inventario.Domain.Shared.Contracts;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Api.Services;

public sealed class ReservationService(
    IDataContext dataContext,
    IHttpContextAccessor httpContextAccessor,
    InventoryAllocationService allocationService)
{
    public async Task<Result<List<Reservation>>> GetReservationsAsync(
        GetReservationsRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<List<Reservation>>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var tenantId = tenantResult.Data;
        var query = dataContext.Query<Reservation>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted);

        if (request.ProductId is { } productId)
            query = query.Where(x => x.ProductId == productId);

        if (request.Status is { } status)
            query = query.Where(x => x.Status == status);

        if (!string.IsNullOrWhiteSpace(request.ReferenceType))
            query = query.Where(x => x.ReferenceType == request.ReferenceType);

        if (request.ReferenceId is { } referenceId)
            query = query.Where(x => x.ReferenceId == referenceId);

        var reservations = await query
            .OrderByDescending(x => x.ReservedAt)
            .Take(500)
            .ToListAsync(ct);

        return Result<List<Reservation>>.Success(reservations);
    }

    public async Task<Result<Reservation>> ReserveAsync(
        ReserveInventoryRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<Reservation>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        if (request.Quantity <= 0)
            return Result<Reservation>.Failure("Reservation quantity must be greater than zero.", 400);

        var reservationId = Guid.NewGuid();
        var allocationsResult = await allocationService.ReserveAsync(tenantResult.Data, reservationId, request, ct);
        if (!allocationsResult.IsSuccess)
            return Result<Reservation>.Failure(allocationsResult.Message!, allocationsResult.StatusCode);

        var now = DateTime.UtcNow;
        var allocations = allocationsResult.Data!;
        var reservation = new Reservation
        {
            Id = reservationId,
            TenantId = tenantResult.Data,
            ProductId = request.ProductId,
            WarehouseId = request.WarehouseId,
            LocationId = request.LocationId,
            StockBalanceId = allocations.FirstOrDefault()?.StockBalanceId,
            Quantity = request.Quantity,
            Status = ReservationStatus.Active,
            ReferenceType = NormalizeOptional(request.ReferenceType),
            ReferenceId = request.ReferenceId,
            ReservedAt = now,
            ExpiresAt = request.ExpiresAt,
            IsEnabled = true,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid(),
            Allocations = allocations
        };

        dataContext.Add(reservation);
        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            return Result<Reservation>.Failure(saveResult.Message ?? "Reservation save failed.", saveResult.StatusCode);

        return Result<Reservation>.Success(reservation, 201, "Reservation created.");
    }

    public async Task<Result<Reservation>> ReleaseAsync(
        ReleaseReservationRequest request,
        CancellationToken ct = default)
    {
        var reservationResult = await GetActiveReservation(request, request.ReservationId, ct);
        if (!reservationResult.IsSuccess)
            return reservationResult;

        var reservation = reservationResult.Data!;
        var releaseResult = await allocationService.ReleaseAsync(
            reservation,
            request,
            ReservationAllocationStatus.Released,
            request.Reason ?? "Reservation released",
            ct);
        if (!releaseResult.IsSuccess)
            return Result<Reservation>.Failure(releaseResult.Message!, releaseResult.StatusCode);
        reservation.Allocations = releaseResult.Data!;

        CompleteReservation(reservation, ReservationStatus.Released, releasedAt: DateTime.UtcNow);

        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            return Result<Reservation>.Failure(saveResult.Message ?? "Reservation release failed.", saveResult.StatusCode);

        return Result<Reservation>.Success(reservation, "Reservation released.");
    }

    public async Task<Result<Reservation>> FulfillAsync(
        FulfillReservationRequest request,
        CancellationToken ct = default)
    {
        var reservationResult = await GetActiveReservation(request, request.ReservationId, ct);
        if (!reservationResult.IsSuccess)
            return reservationResult;

        var reservation = reservationResult.Data!;
        var fulfillmentResult = await allocationService.FulfillAsync(
            reservation,
            request,
            "Reservation fulfilled: release reserved quantity",
            request.Reason ?? "Reservation fulfilled",
            ct);
        if (!fulfillmentResult.IsSuccess)
            return Result<Reservation>.Failure(fulfillmentResult.Message!, fulfillmentResult.StatusCode);
        reservation.Allocations = fulfillmentResult.Data!;

        CompleteReservation(reservation, ReservationStatus.Fulfilled, fulfilledAt: DateTime.UtcNow);

        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            return Result<Reservation>.Failure(saveResult.Message ?? "Reservation fulfillment failed.", saveResult.StatusCode);

        return Result<Reservation>.Success(reservation, "Reservation fulfilled.");
    }

    public async Task<Result<Reservation>> CancelAsync(
        CancelReservationRequest request,
        CancellationToken ct = default)
    {
        var reservationResult = await GetActiveReservation(request, request.ReservationId, ct);
        if (!reservationResult.IsSuccess)
            return reservationResult;

        var reservation = reservationResult.Data!;
        var releaseResult = await allocationService.ReleaseAsync(
            reservation,
            request,
            ReservationAllocationStatus.Cancelled,
            request.Reason ?? "Reservation cancelled",
            ct);
        if (!releaseResult.IsSuccess)
            return Result<Reservation>.Failure(releaseResult.Message!, releaseResult.StatusCode);
        reservation.Allocations = releaseResult.Data!;

        CompleteReservation(reservation, ReservationStatus.Cancelled, releasedAt: DateTime.UtcNow);

        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            return Result<Reservation>.Failure(saveResult.Message ?? "Reservation cancellation failed.", saveResult.StatusCode);

        return Result<Reservation>.Success(reservation, "Reservation cancelled.");
    }

    public async Task<Result<int>> ExpireAsync(
        ExpireReservationsRequest request,
        CancellationToken ct = default)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<int>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var cutoff = request.ExpiresBefore ?? DateTime.UtcNow;
        var maxCount = Math.Clamp(request.MaxCount, 1, 500);
        var reservations = await dataContext.Query<Reservation>()
            .IgnoreQueryFilters()
            .Where(x =>
                x.TenantId == tenantResult.Data &&
                x.Status == ReservationStatus.Active &&
                x.ExpiresAt.HasValue &&
                x.ExpiresAt.Value <= cutoff &&
                !x.IsDeleted)
            .OrderBy(x => x.ExpiresAt)
            .Take(maxCount)
            .ToListAsync(ct);

        if (reservations.Count == 0)
            return Result<int>.Success(0, "No reservations expired.");

        foreach (var reservation in reservations)
        {
            var releaseResult = await allocationService.ReleaseAsync(
                reservation,
                request,
                ReservationAllocationStatus.Expired,
                "Reservation expired",
                ct);
            if (!releaseResult.IsSuccess)
                return Result<int>.Failure(releaseResult.Message!, releaseResult.StatusCode);
            reservation.Allocations = releaseResult.Data!;

            CompleteReservation(reservation, ReservationStatus.Expired, releasedAt: DateTime.UtcNow);
        }

        var saveResult = await dataContext.SaveChangesAsync(ct);
        if (!saveResult.IsSuccess)
            return Result<int>.Failure(saveResult.Message ?? "Reservation expiration failed.", saveResult.StatusCode);

        return Result<int>.Success(reservations.Count, $"{reservations.Count} reservation(s) expired.");
    }

    private async Task<Result<Reservation>> GetActiveReservation(
        RequestBase request,
        Guid reservationId,
        CancellationToken ct)
    {
        var tenantResult = GetCurrentTenantId(request);
        if (!tenantResult.IsSuccess)
            return Result<Reservation>.Failure(tenantResult.Message!, tenantResult.StatusCode);

        var reservation = await dataContext.Query<Reservation>()
            .IgnoreQueryFilters()
            .Where(x =>
                x.TenantId == tenantResult.Data &&
                x.Id == reservationId &&
                x.Status == ReservationStatus.Active &&
                !x.IsDeleted)
            .FirstOrDefaultAsync(ct);

        return reservation is null
            ? Result<Reservation>.NotFound("Active reservation not found.")
            : Result<Reservation>.Success(reservation);
    }

    private void CompleteReservation(
        Reservation reservation,
        ReservationStatus status,
        DateTime? releasedAt = null,
        DateTime? fulfilledAt = null)
    {
        reservation.Status = status;
        reservation.ReleasedAt = releasedAt;
        reservation.FulfilledAt = fulfilledAt;
        reservation.ModifiedAt = DateTime.UtcNow;
        reservation.ConcurrencyStamp = Guid.NewGuid();
        dataContext.Update(reservation);
    }

    private Result<Guid> GetCurrentTenantId(RequestBase? request)
    {
        if (request?.Metadata?.TenantId is { } metadataTenantId && metadataTenantId != Guid.Empty)
            return Result<Guid>.Success(metadataTenantId);

        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return Result<Guid>.Unauthorized("Authentication is required for reservation operations.");

        var tenantIdClaim = user.FindFirst("tenantId")?.Value
            ?? user.FindFirst("TenantId")?.Value
            ?? user.FindFirst("tid")?.Value;

        if (Guid.TryParse(tenantIdClaim, out var tenantId) && tenantId != Guid.Empty)
            return Result<Guid>.Success(tenantId);

        return Result<Guid>.Forbidden("Authenticated user does not have a valid tenant context.");
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
