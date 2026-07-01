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
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;
using XFramework.Inventario.Domain.Shared.Enums;
using Inventario.Integration.Drivers;

namespace POS.Api.Services;

public sealed class PosReturnsService(
    AppDbContext db,
    IInventarioServiceWrapper inventario,
    IWalletsServiceWrapper wallets)
{
    public async Task<Result<PosReturnResponse>> CreateAsync(
        CreatePosReturnRequest request,
        CancellationToken ct)
    {
        if (!PosServiceHelpers.TryResolveTenantId(request.Metadata, out var tenantId))
            return Result<PosReturnResponse>.Failure("Tenant ID is required", 400);

        var idempotencyKey = PosServiceHelpers.NormalizeOptional(request.IdempotencyKey)
            ?? $"POS.Return.{Guid.NewGuid():N}";

        var replay = await LoadReturnByIdempotencyAsync(tenantId, idempotencyKey, ct);
        if (replay is not null)
            return Result<PosReturnResponse>.Success(PosServiceHelpers.ToReturnResponse(replay), "POS return replayed");

        if (request.Lines.Count == 0)
            return Result<PosReturnResponse>.Failure("At least one return line is required", 400);

        var sale = await db.Set<PosSale>()
            .AsTracking()
            .Include(item => item.Register)
            .Include(item => item.Lines)
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                item.Id == request.SaleId &&
                !item.IsDeleted,
                ct);

        if (sale is null)
            return Result<PosReturnResponse>.NotFound("POS sale was not found");

        if (sale.Status != PosSaleStatus.Completed)
            return Result<PosReturnResponse>.Conflict("Only completed POS sales can be returned");

        var now = DateTime.UtcNow;
        var posReturn = new PosReturn
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReturnNumber = string.Empty,
            SaleId = sale.Id,
            RegisterId = sale.RegisterId,
            CashierCredentialId = request.CashierCredentialId,
            CustomerCredentialId = sale.CustomerCredentialId,
            RefundMethod = request.RefundMethod,
            Status = PosReturnStatus.Pending,
            CurrencyId = sale.CurrencyId,
            WalletTypeId = sale.WalletTypeId,
            Reason = PosServiceHelpers.NormalizeOptional(request.Reason),
            IdempotencyKey = idempotencyKey,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid(),
            IsEnabled = true
        };
        posReturn.ReturnNumber = PosServiceHelpers.NewReturnNumber(now, posReturn.Id);
        posReturn.Lines = await BuildReturnLinesAsync(posReturn, sale, request, tenantId, ct);

        if (posReturn.Lines.Count != request.Lines.Count)
            return Result<PosReturnResponse>.Failure("One or more return lines are invalid", 400);

        posReturn.SubtotalAmount = posReturn.Lines.Sum(line => line.Quantity * line.UnitPrice);
        posReturn.TaxAmount = posReturn.Lines.Sum(line => line.TaxAmount);
        posReturn.TotalRefundAmount = posReturn.SubtotalAmount + posReturn.TaxAmount;

        db.Set<PosReturn>().Add(posReturn);
        await db.SaveChangesAsync(ct);

        var inventoryResult = await PostReturnInventoryAsync(posReturn, request.Metadata);
        if (!inventoryResult.IsSuccess)
        {
            posReturn.Status = PosReturnStatus.Failed;
            posReturn.FailureReason = inventoryResult.Message;
            await db.SaveChangesAsync(ct);
            return Result<PosReturnResponse>.Success(PosServiceHelpers.ToReturnResponse(posReturn), inventoryResult.Message);
        }

        posReturn.Status = PosReturnStatus.InventoryPosted;
        await db.SaveChangesAsync(ct);

        var refundResult = await RefundAsync(posReturn, sale.Register, request.Metadata);
        if (!refundResult.IsSuccess)
        {
            posReturn.Status = PosReturnStatus.Failed;
            posReturn.FailureReason = refundResult.Message;
            await db.SaveChangesAsync(ct);
            return Result<PosReturnResponse>.Success(PosServiceHelpers.ToReturnResponse(posReturn), refundResult.Message);
        }

        posReturn.Status = PosReturnStatus.Completed;
        posReturn.CompletedAt = DateTime.UtcNow;
        posReturn.FailureReason = null;
        await db.SaveChangesAsync(ct);

        return Result<PosReturnResponse>.Success(PosServiceHelpers.ToReturnResponse(posReturn), 201, "POS return completed");
    }

    public async Task<Result<PosReturnResponse>> GetAsync(
        GetPosReturnRequest request,
        CancellationToken ct)
    {
        if (!PosServiceHelpers.TryResolveTenantId(request.Metadata, out var tenantId))
            return Result<PosReturnResponse>.Failure("Tenant ID is required", 400);

        var posReturn = await LoadReturnAsync(tenantId, request.Id, false, ct);
        return posReturn is null
            ? Result<PosReturnResponse>.NotFound("POS return was not found")
            : Result<PosReturnResponse>.Success(PosServiceHelpers.ToReturnResponse(posReturn));
    }

    public async Task<Result<List<PosReturnSummaryResponse>>> SearchAsync(
        SearchPosReturnsRequest request,
        CancellationToken ct)
    {
        if (!PosServiceHelpers.TryResolveTenantId(request.Metadata, out var tenantId))
            return Result<List<PosReturnSummaryResponse>>.Failure("Tenant ID is required", 400);

        var (page, pageSize) = PosServiceHelpers.NormalizePage(request.Page, request.PageSize);
        IQueryable<PosReturn> query = db.Set<PosReturn>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted);

        if (request.SaleId.HasValue)
            query = query.Where(item => item.SaleId == request.SaleId.Value);

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
                item.ReturnNumber.Contains(search) ||
                item.IdempotencyKey.Contains(search));
        }

        var items = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Result<List<PosReturnSummaryResponse>>.Success(
            items.Select(PosServiceHelpers.ToReturnSummaryResponse).ToList());
    }

    private async Task<List<PosReturnLine>> BuildReturnLinesAsync(
        PosReturn posReturn,
        PosSale sale,
        CreatePosReturnRequest request,
        Guid tenantId,
        CancellationToken ct)
    {
        var lines = new List<PosReturnLine>();

        foreach (var requestLine in request.Lines)
        {
            var saleLine = sale.Lines.FirstOrDefault(item => item.Id == requestLine.SaleLineId);
            if (saleLine is null || requestLine.Quantity <= 0)
                continue;

            var returnedQuantity = await db.Set<PosReturnLine>()
                .AsNoTracking()
                .Where(item =>
                    item.TenantId == tenantId &&
                    item.SaleLineId == saleLine.Id &&
                    !item.IsDeleted)
                .SumAsync(item => item.Quantity, ct);

            if (returnedQuantity + requestLine.Quantity > saleLine.Quantity)
                continue;

            var refundAmount = requestLine.Quantity * saleLine.UnitPrice + requestLine.TaxAmount;
            lines.Add(new PosReturnLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ReturnId = posReturn.Id,
                SaleLineId = saleLine.Id,
                ProductId = saleLine.ProductId,
                ProductVariationId = saleLine.ProductVariationId,
                ProductName = saleLine.ProductName,
                VariantName = saleLine.VariantName,
                Quantity = requestLine.Quantity,
                UnitPrice = saleLine.UnitPrice,
                TaxAmount = requestLine.TaxAmount,
                RefundAmount = refundAmount,
                WarehouseId = saleLine.WarehouseId,
                LocationId = saleLine.LocationId,
                LotId = saleLine.LotId,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid(),
                IsEnabled = true
            });
        }

        return lines;
    }

    private async Task<Result> PostReturnInventoryAsync(
        PosReturn posReturn,
        RequestMetadata metadata)
    {
        foreach (var line in posReturn.Lines)
        {
            var idempotencyKey = $"POS.ReturnLine.{line.Id:N}";
            var response = await inventario.PostStockMovement(new PostStockMovementRequest
            {
                ProductId = line.ProductId,
                ProductVariationId = line.ProductVariationId,
                WarehouseId = line.WarehouseId,
                LocationId = line.LocationId,
                LotId = line.LotId,
                MovementType = InventoryMovementType.Return,
                Quantity = line.Quantity,
                ReferenceType = PosServiceHelpers.ReturnLineReferenceType,
                ReferenceId = line.Id,
                Reason = posReturn.Reason ?? $"POS return {posReturn.ReturnNumber}",
                IdempotencyKey = idempotencyKey,
                Metadata = metadata
            });

            if (!response.IsSuccess)
            {
                line.FailureReason = response.Message ?? "Inventory return posting failed";
                return Result.Failure(line.FailureReason, (int)response.HttpStatusCode);
            }

            line.InventoryMovementReferenceNumber = idempotencyKey;
            line.FailureReason = null;
        }

        return Result.Success();
    }

    private async Task<Result> RefundAsync(
        PosReturn posReturn,
        PosRegister register,
        RequestMetadata metadata)
    {
        var reference = PosServiceHelpers.ReturnRefundReference(posReturn);
        posReturn.RefundReferenceNumber = reference;

        if (posReturn.RefundMethod == PosPaymentMethod.CashDrawer)
        {
            var response = await wallets.DecrementWallet(new DecrementWalletRequest
            {
                CredentialId = register.MerchantCredentialId,
                WalletId = register.CashDrawerWalletId,
                WalletTypeId = posReturn.WalletTypeId,
                CurrencyId = posReturn.CurrencyId,
                Amount = posReturn.TotalRefundAmount,
                Remarks = $"POS refund {posReturn.ReturnNumber}",
                ReferenceNumber = reference,
                IdempotencyKey = reference,
                Metadata = metadata
            });

            return response.IsSuccess
                ? Result.Success()
                : Result.Failure(response.Message ?? "Cash drawer refund failed", (int)response.HttpStatusCode);
        }

        if (posReturn.CustomerCredentialId is not { } customerCredentialId || customerCredentialId == Guid.Empty)
            return Result.Failure("Customer credential is required for wallet refund", 400);

        var transfer = await wallets.TransferWallet(new TransferWalletRequest
        {
            CredentialId = register.MerchantCredentialId,
            RecipientCredentialId = customerCredentialId,
            WalletTypeId = posReturn.WalletTypeId,
            CurrencyId = posReturn.CurrencyId,
            Amount = posReturn.TotalRefundAmount,
            Remarks = $"POS refund {posReturn.ReturnNumber}",
            ReferenceNumber = reference,
            IdempotencyKey = reference,
            TransactionPurpose = TransactionPurpose.Refund,
            Metadata = metadata
        });

        return transfer.IsSuccess
            ? Result.Success()
            : Result.Failure(transfer.Message ?? "Wallet refund transfer failed", (int)transfer.HttpStatusCode);
    }

    private async Task<PosReturn?> LoadReturnAsync(
        Guid tenantId,
        Guid returnId,
        bool tracking,
        CancellationToken ct)
    {
        var query = db.Set<PosReturn>()
            .Include(item => item.Sale)
            .Include(item => item.Lines)
            .Where(item => item.TenantId == tenantId && item.Id == returnId && !item.IsDeleted);

        if (!tracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(ct);
    }

    private async Task<PosReturn?> LoadReturnByIdempotencyAsync(
        Guid tenantId,
        string idempotencyKey,
        CancellationToken ct) =>
        await db.Set<PosReturn>()
            .AsNoTracking()
            .Include(item => item.Sale)
            .Include(item => item.Lines)
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                item.IdempotencyKey == idempotencyKey &&
                !item.IsDeleted,
                ct);
}
