using Microsoft.EntityFrameworkCore;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Contracts.Responses;
using IdentityServer.Domain.Shared.Contracts;
using Wallets.Domain.Shared.Contracts;
using XFramework.Core.Patterns;
using XFramework.Domain.Contexts;
using XFramework.Inventario.Domain.Shared.Contracts;

namespace POS.Api.Services;

public sealed class PosRegisterService(AppDbContext db, IPosRequestContextResolver contextResolver)
{
    public async Task<Result<PosRegisterResponse>> GetAsync(
        GetPosRegisterRequest request,
        CancellationToken ct)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess)
            return Result<PosRegisterResponse>.Failure(contextResult.Message!, contextResult.StatusCode);

        var tenantId = contextResult.Data!.TenantId;

        var register = await db.Set<PosRegister>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                item.Id == request.Id &&
                !item.IsDeleted,
                ct);

        return register is null
            ? Result<PosRegisterResponse>.NotFound("POS register was not found")
            : Result<PosRegisterResponse>.Success(PosServiceHelpers.ToRegisterResponse(register));
    }

    public async Task<Result<PosRegisterResponse>> CreateAsync(
        CreatePosRegisterRequest request,
        CancellationToken ct)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess)
            return Result<PosRegisterResponse>.Failure(contextResult.Message!, contextResult.StatusCode);

        var tenantId = contextResult.Data!.TenantId;
        var referenceValidation = await ValidateRegisterReferencesAsync(
            tenantId,
            request.MerchantCredentialId,
            request.CashDrawerWalletId,
            request.WalletTypeId,
            request.CurrencyId,
            request.DefaultWarehouseId,
            request.DefaultLocationId,
            ct);
        if (!referenceValidation.IsSuccess)
            return Result<PosRegisterResponse>.Failure(referenceValidation.Message!, referenceValidation.StatusCode);

        var normalizedCode = PosServiceHelpers.NormalizeOptional(request.Code);
        if (normalizedCode is not null)
        {
            var duplicate = await db.Set<PosRegister>()
                .AsNoTracking()
                .AnyAsync(item =>
                    item.TenantId == tenantId &&
                    item.Code == normalizedCode &&
                    !item.IsDeleted,
                    ct);

            if (duplicate)
                return Result<PosRegisterResponse>.Conflict("A POS register with this code already exists");
        }

        var register = new PosRegister
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Code = normalizedCode,
            MerchantCredentialId = request.MerchantCredentialId,
            CashDrawerWalletId = request.CashDrawerWalletId,
            WalletTypeId = request.WalletTypeId,
            CurrencyId = request.CurrencyId,
            DefaultWarehouseId = request.DefaultWarehouseId,
            DefaultLocationId = request.DefaultLocationId,
            Description = PosServiceHelpers.NormalizeOptional(request.Description),
            IsEnabled = request.IsEnabled,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };

        db.Set<PosRegister>().Add(register);
        await db.SaveChangesAsync(ct);

        return Result<PosRegisterResponse>.Success(
            PosServiceHelpers.ToRegisterResponse(register),
            201,
            "POS register created");
    }

    public async Task<Result<PosRegisterResponse>> UpdateAsync(
        UpdatePosRegisterRequest request,
        CancellationToken ct)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess)
            return Result<PosRegisterResponse>.Failure(contextResult.Message!, contextResult.StatusCode);

        var tenantId = contextResult.Data!.TenantId;

        var register = await db.Set<PosRegister>()
            .AsTracking()
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                item.Id == request.Id &&
                !item.IsDeleted,
                ct);

        if (register is null)
            return Result<PosRegisterResponse>.NotFound("POS register was not found");

        var referenceValidation = await ValidateRegisterReferencesAsync(
            tenantId,
            request.MerchantCredentialId,
            request.CashDrawerWalletId,
            request.WalletTypeId,
            request.CurrencyId,
            request.DefaultWarehouseId,
            request.DefaultLocationId,
            ct);
        if (!referenceValidation.IsSuccess)
            return Result<PosRegisterResponse>.Failure(referenceValidation.Message!, referenceValidation.StatusCode);

        var normalizedCode = PosServiceHelpers.NormalizeOptional(request.Code);
        if (normalizedCode is not null)
        {
            var duplicate = await db.Set<PosRegister>()
                .AsNoTracking()
                .AnyAsync(item =>
                    item.TenantId == tenantId &&
                    item.Id != request.Id &&
                    item.Code == normalizedCode &&
                    !item.IsDeleted,
                    ct);

            if (duplicate)
                return Result<PosRegisterResponse>.Conflict("A POS register with this code already exists");
        }

        register.Name = request.Name.Trim();
        register.Code = normalizedCode;
        register.MerchantCredentialId = request.MerchantCredentialId;
        register.CashDrawerWalletId = request.CashDrawerWalletId;
        register.WalletTypeId = request.WalletTypeId;
        register.CurrencyId = request.CurrencyId;
        register.DefaultWarehouseId = request.DefaultWarehouseId;
        register.DefaultLocationId = request.DefaultLocationId;
        register.Description = PosServiceHelpers.NormalizeOptional(request.Description);
        register.IsEnabled = request.IsEnabled;
        register.ModifiedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Result<PosRegisterResponse>.Success(
            PosServiceHelpers.ToRegisterResponse(register),
            "POS register updated");
    }

    private async Task<Result> ValidateRegisterReferencesAsync(
        Guid tenantId,
        Guid merchantCredentialId,
        Guid cashDrawerWalletId,
        Guid walletTypeId,
        Guid currencyId,
        Guid warehouseId,
        Guid locationId,
        CancellationToken ct)
    {
        var merchantExists = await db.Set<IdentityCredential>()
            .AsNoTracking()
            .AnyAsync(item => item.TenantId == tenantId && item.Id == merchantCredentialId && !item.IsDeleted, ct);
        if (!merchantExists)
            return Result.NotFound("Merchant credential was not found for this tenant");

        var walletType = await db.Set<WalletType>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == walletTypeId && !item.IsDeleted, ct);
        if (walletType is null)
            return Result.NotFound("Wallet type was not found for this tenant");

        if (walletType.CurrencyTypeId.HasValue && walletType.CurrencyTypeId.Value != currencyId)
            return Result.Conflict("Wallet type currency does not match the POS register currency");

        var currencyExists = await db.Set<CurrencyType>()
            .AsNoTracking()
            .AnyAsync(item => item.TenantId == tenantId && item.Id == currencyId && !item.IsDeleted, ct);
        if (!currencyExists)
            return Result.NotFound("Currency was not found for this tenant");

        var cashDrawerWallet = await db.Set<Wallet>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == cashDrawerWalletId && !item.IsDeleted, ct);
        if (cashDrawerWallet is null)
            return Result.NotFound("Cash drawer wallet was not found for this tenant");

        if (cashDrawerWallet.WalletTypeId.HasValue && cashDrawerWallet.WalletTypeId.Value != walletTypeId)
            return Result.Conflict("Cash drawer wallet type does not match the POS register wallet type");

        var warehouseExists = await db.Set<Warehouse>()
            .AsNoTracking()
            .AnyAsync(item => item.TenantId == tenantId && item.Id == warehouseId && !item.IsDeleted, ct);
        if (!warehouseExists)
            return Result.NotFound("Warehouse was not found for this tenant");

        var locationExists = await db.Set<InventoryLocation>()
            .AsNoTracking()
            .AnyAsync(item =>
                item.TenantId == tenantId &&
                item.Id == locationId &&
                item.WarehouseId == warehouseId &&
                !item.IsDeleted,
                ct);
        if (!locationExists)
            return Result.NotFound("Location was not found in the selected warehouse for this tenant");

        return Result.Success();
    }
}
