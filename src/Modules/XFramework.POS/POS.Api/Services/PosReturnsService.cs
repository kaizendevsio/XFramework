using System.Data;
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
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Stock;
using XFramework.Inventario.Domain.Shared.Enums;

namespace POS.Api.Services;

public sealed class PosReturnsService(
    AppDbContext db,
    IInventarioServiceWrapper inventario,
    IWalletsServiceWrapper wallets,
    IPosRequestContextResolver contextResolver)
{
    public async Task<Result<PosReturnResponse>> CreateAsync(
        CreatePosReturnRequest request,
        CancellationToken ct)
    {
        var contextResult = contextResolver.Resolve(request, request.CashierCredentialId);
        if (!contextResult.IsSuccess)
            return Result<PosReturnResponse>.Failure(contextResult.Message!, contextResult.StatusCode);

        var idempotencyKey = PosServiceHelpers.NormalizeOptional(request.IdempotencyKey) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Result<PosReturnResponse>.Failure("Return idempotency key is required", 400);

        var context = contextResult.Data!;
        var requestHash = PosServiceHelpers.BuildReturnRequestHash(request);
        var replay = await LoadReturnByIdempotencyAsync(context.TenantId, idempotencyKey, tracking: true, ct);
        if (replay is not null)
        {
            if (!string.Equals(replay.RequestHash, requestHash, StringComparison.Ordinal))
                return Result<PosReturnResponse>.Conflict("Return idempotency key was reused with a different payload");

            return await ExecuteReturnWorkflowAsync(replay, context.Metadata, ct, replayed: true);
        }

        if (request.Lines.Count == 0)
            return Result<PosReturnResponse>.Failure("At least one return line is required", 400);

        if (request.Lines.Select(line => line.SaleLineId).Distinct().Count() != request.Lines.Count)
            return Result<PosReturnResponse>.Failure("A sale line can appear only once in a POS return", 400);

        if (request.Lines.Any(line => line.TaxAmount != 0))
            return Result<PosReturnResponse>.Failure("Return tax is calculated from the original sale", 400);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var sale = await db.Set<PosSale>()
            .AsTracking()
            .Include(item => item.Register)
            .Include(item => item.Lines)
            .Include(item => item.Payments)
            .FirstOrDefaultAsync(item =>
                item.TenantId == context.TenantId &&
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
            TenantId = context.TenantId,
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
            RequestHash = requestHash,
            CreatedAt = now,
            ConcurrencyStamp = Guid.NewGuid(),
            IsEnabled = true,
            Sale = sale,
            Register = sale.Register
        };
        posReturn.ReturnNumber = PosServiceHelpers.NewReturnNumber(now, posReturn.Id);
        var lineResult = await BuildReturnLinesAsync(posReturn, sale, request, context.TenantId, ct);
        if (!lineResult.IsSuccess)
            return Result<PosReturnResponse>.Failure(lineResult.Message!, lineResult.StatusCode);

        posReturn.Lines = lineResult.Data!;
        posReturn.TaxAmount = posReturn.Lines.Sum(line => line.TaxAmount);
        posReturn.TotalRefundAmount = posReturn.Lines.Sum(line => line.RefundAmount);
        posReturn.SubtotalAmount = posReturn.TotalRefundAmount - posReturn.TaxAmount;

        var capturedAmount = sale.Payments
            .Where(payment => payment.Status == PosPaymentStatus.Captured)
            .Sum(payment => payment.Amount);
        var saleLineIds = sale.Lines.Select(line => line.Id).ToList();
        var previouslyAllocatedRefund = await db.Set<PosReturnLine>()
            .AsNoTracking()
            .Where(line =>
                line.TenantId == context.TenantId &&
                saleLineIds.Contains(line.SaleLineId) &&
                !line.IsDeleted)
            .SumAsync(line => line.RefundAmount, ct);

        if (previouslyAllocatedRefund + posReturn.TotalRefundAmount > capturedAmount)
            return Result<PosReturnResponse>.Conflict("Return total exceeds the captured payment amount");

        db.Set<PosReturn>().Add(posReturn);
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            var concurrentReplay = await LoadReturnByIdempotencyAsync(
                context.TenantId,
                idempotencyKey,
                tracking: true,
                ct);
            if (concurrentReplay is null)
                throw;

            if (!string.Equals(concurrentReplay.RequestHash, requestHash, StringComparison.Ordinal))
                return Result<PosReturnResponse>.Conflict("Return idempotency key was reused with a different payload");

            return await ExecuteReturnWorkflowAsync(concurrentReplay, context.Metadata, ct, replayed: true);
        }

        return await ExecuteReturnWorkflowAsync(posReturn, context.Metadata, ct, replayed: false);
    }

    public async Task<Result<PosReturnResponse>> RetryAsync(
        RetryPosReturnRequest request,
        CancellationToken ct)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess)
            return Result<PosReturnResponse>.Failure(contextResult.Message!, contextResult.StatusCode);

        var posReturn = await LoadReturnAsync(contextResult.Data!.TenantId, request.ReturnId, tracking: true, ct);
        if (posReturn is null)
            return Result<PosReturnResponse>.NotFound("POS return was not found");

        if (posReturn.Status == PosReturnStatus.Completed)
            return Result<PosReturnResponse>.Success(PosServiceHelpers.ToReturnResponse(posReturn), "POS return already completed");

        if (posReturn.Status is not PosReturnStatus.Pending
            and not PosReturnStatus.InventoryPostFailed
            and not PosReturnStatus.InventoryPosted
            and not PosReturnStatus.RefundFailed
            and not PosReturnStatus.Failed)
        {
            return Result<PosReturnResponse>.Conflict("POS return is not in a retryable state");
        }

        return await ExecuteReturnWorkflowAsync(posReturn, contextResult.Data.Metadata, ct, replayed: true);
    }

    public async Task<Result<PosReturnResponse>> GetAsync(
        GetPosReturnRequest request,
        CancellationToken ct)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess)
            return Result<PosReturnResponse>.Failure(contextResult.Message!, contextResult.StatusCode);

        var posReturn = await LoadReturnAsync(contextResult.Data!.TenantId, request.Id, false, ct);
        return posReturn is null
            ? Result<PosReturnResponse>.NotFound("POS return was not found")
            : Result<PosReturnResponse>.Success(PosServiceHelpers.ToReturnResponse(posReturn));
    }

    public async Task<Result<List<PosReturnSummaryResponse>>> SearchAsync(
        SearchPosReturnsRequest request,
        CancellationToken ct)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess)
            return Result<List<PosReturnSummaryResponse>>.Failure(contextResult.Message!, contextResult.StatusCode);

        var tenantId = contextResult.Data!.TenantId;
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

    private async Task<Result<PosReturnResponse>> ExecuteReturnWorkflowAsync(
        PosReturn posReturn,
        RequestMetadata metadata,
        CancellationToken ct,
        bool replayed)
    {
        if (posReturn.Status is PosReturnStatus.Pending or PosReturnStatus.InventoryPostFailed or PosReturnStatus.Failed)
        {
            var inventoryResult = await PostReturnInventoryAsync(posReturn, metadata, ct);
            if (!inventoryResult.IsSuccess)
            {
                posReturn.Status = PosReturnStatus.InventoryPostFailed;
                posReturn.FailureReason = inventoryResult.Message;
                await db.SaveChangesAsync(ct);
                return Result<PosReturnResponse>.Success(PosServiceHelpers.ToReturnResponse(posReturn), inventoryResult.Message);
            }

            posReturn.Status = PosReturnStatus.InventoryPosted;
            posReturn.FailureReason = null;
            await db.SaveChangesAsync(ct);
        }

        if (posReturn.Status is PosReturnStatus.InventoryPosted or PosReturnStatus.RefundFailed)
        {
            var refundResult = await RefundAsync(posReturn, posReturn.Register, metadata);
            if (!refundResult.IsSuccess)
            {
                posReturn.Status = PosReturnStatus.RefundFailed;
                posReturn.FailureReason = refundResult.Message;
                await db.SaveChangesAsync(ct);
                return Result<PosReturnResponse>.Success(PosServiceHelpers.ToReturnResponse(posReturn), refundResult.Message);
            }

            posReturn.Status = PosReturnStatus.Completed;
            posReturn.CompletedAt = DateTime.UtcNow;
            posReturn.FailureReason = null;
            await db.SaveChangesAsync(ct);
        }

        var response = PosServiceHelpers.ToReturnResponse(posReturn);
        return replayed
            ? Result<PosReturnResponse>.Success(response, "POS return replay completed")
            : Result<PosReturnResponse>.Success(response, 201, "POS return completed");
    }

    private async Task<Result<List<PosReturnLine>>> BuildReturnLinesAsync(
        PosReturn posReturn,
        PosSale sale,
        CreatePosReturnRequest request,
        Guid tenantId,
        CancellationToken ct)
    {
        var lines = new List<PosReturnLine>();
        var saleLineIds = sale.Lines.Select(line => line.Id).ToList();
        var previousReturns = await db.Set<PosReturnLine>()
            .AsNoTracking()
            .Where(line =>
                line.TenantId == tenantId &&
                saleLineIds.Contains(line.SaleLineId) &&
                !line.IsDeleted)
            .GroupBy(line => line.SaleLineId)
            .Select(group => new
            {
                SaleLineId = group.Key,
                Quantity = group.Sum(line => line.Quantity),
                TaxAmount = group.Sum(line => line.TaxAmount),
                RefundAmount = group.Sum(line => line.RefundAmount)
            })
            .ToDictionaryAsync(item => item.SaleLineId, ct);
        var allocations = PosServiceHelpers.BuildSaleRefundAllocations(sale);

        foreach (var requestLine in request.Lines)
        {
            var saleLine = sale.Lines.FirstOrDefault(item => item.Id == requestLine.SaleLineId);
            if (saleLine is null)
                return Result<List<PosReturnLine>>.NotFound("POS sale line was not found");

            if (requestLine.Quantity <= 0)
                return Result<List<PosReturnLine>>.Failure("Return quantity must be greater than zero", 400);

            previousReturns.TryGetValue(saleLine.Id, out var previous);
            var returnedQuantity = previous?.Quantity ?? 0;
            var remainingQuantity = saleLine.Quantity - returnedQuantity;
            if (requestLine.Quantity > remainingQuantity)
                return Result<List<PosReturnLine>>.Conflict("Return quantity exceeds the remaining sale line quantity");

            var allocation = allocations[saleLine.Id];
            var remainingRefund = allocation.RefundAmount - (previous?.RefundAmount ?? 0);
            var remainingTax = allocation.TaxAmount - (previous?.TaxAmount ?? 0);
            var partialAllocation = PosServiceHelpers.BuildPartialReturnAllocation(
                allocation,
                saleLine.Quantity,
                returnedQuantity,
                previous?.TaxAmount ?? 0,
                previous?.RefundAmount ?? 0,
                requestLine.Quantity);
            var refundAmount = partialAllocation.RefundAmount;
            var taxAmount = partialAllocation.TaxAmount;

            if (refundAmount < 0 || refundAmount > remainingRefund || taxAmount > remainingTax)
                return Result<List<PosReturnLine>>.Conflict("Return amount exceeds the remaining refundable amount");

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
                TaxAmount = taxAmount,
                RefundAmount = refundAmount,
                WarehouseId = saleLine.WarehouseId,
                LocationId = saleLine.LocationId,
                LotId = saleLine.LotId,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid(),
                IsEnabled = true
            });
        }

        return Result<List<PosReturnLine>>.Success(lines);
    }

    private async Task<Result> PostReturnInventoryAsync(
        PosReturn posReturn,
        RequestMetadata metadata,
        CancellationToken ct)
    {
        foreach (var line in posReturn.Lines.OrderBy(item => item.CreatedAt))
        {
            if (!string.IsNullOrWhiteSpace(line.InventoryMovementReferenceNumber))
                continue;

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
                await db.SaveChangesAsync(ct);
                return Result.Failure(line.FailureReason, (int)response.HttpStatusCode);
            }

            line.InventoryMovementReferenceNumber = idempotencyKey;
            line.FailureReason = null;
            await db.SaveChangesAsync(ct);
        }

        return Result.Success();
    }

    private async Task<Result> RefundAsync(
        PosReturn posReturn,
        PosRegister register,
        RequestMetadata metadata)
    {
        var reference = PosServiceHelpers.NormalizeOptional(posReturn.RefundReferenceNumber)
            ?? PosServiceHelpers.ReturnRefundReference(posReturn);
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
            .Include(item => item.Register)
            .Include(item => item.Lines)
            .Where(item => item.TenantId == tenantId && item.Id == returnId && !item.IsDeleted);

        if (!tracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(ct);
    }

    private async Task<PosReturn?> LoadReturnByIdempotencyAsync(
        Guid tenantId,
        string idempotencyKey,
        bool tracking,
        CancellationToken ct) =>
        await (tracking
                ? db.Set<PosReturn>().AsTracking()
                : db.Set<PosReturn>().AsNoTracking())
            .Include(item => item.Sale)
            .Include(item => item.Register)
            .Include(item => item.Lines)
            .FirstOrDefaultAsync(item =>
                    item.TenantId == tenantId &&
                    item.IdempotencyKey == idempotencyKey &&
                    !item.IsDeleted,
                ct);
}
