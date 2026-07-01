using Microsoft.EntityFrameworkCore;
using POS.Domain.Shared.Contracts;
using POS.Domain.Shared.Contracts.Requests;
using POS.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Domain.Contexts;

namespace POS.Api.Services;

public sealed class PosRegisterService(AppDbContext db)
{
    public async Task<Result<PosRegisterResponse>> GetAsync(
        GetPosRegisterRequest request,
        CancellationToken ct)
    {
        if (!PosServiceHelpers.TryResolveTenantId(request.Metadata, out var tenantId))
            return Result<PosRegisterResponse>.Failure("Tenant ID is required", 400);

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
        if (!PosServiceHelpers.TryResolveTenantId(request.Metadata, out var tenantId))
            return Result<PosRegisterResponse>.Failure("Tenant ID is required", 400);

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
        if (!PosServiceHelpers.TryResolveTenantId(request.Metadata, out var tenantId))
            return Result<PosRegisterResponse>.Failure("Tenant ID is required", 400);

        var register = await db.Set<PosRegister>()
            .AsTracking()
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                item.Id == request.Id &&
                !item.IsDeleted,
                ct);

        if (register is null)
            return Result<PosRegisterResponse>.NotFound("POS register was not found");

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
}
