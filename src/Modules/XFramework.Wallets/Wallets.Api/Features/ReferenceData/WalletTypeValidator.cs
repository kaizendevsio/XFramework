using FluentValidation;

namespace Wallets.Api.Features.ReferenceData;

public sealed class WalletTypeValidator : AbstractValidator<WalletType>
{
    public WalletTypeValidator(DbContext dbContext)
    {
        RuleFor(type => type.CurrencyTypeId)
            .MustAsync((type, currencyId, ct) => dbContext.Set<CurrencyType>()
                .IgnoreQueryFilters()
                .AnyAsync(currency => currency.Id == currencyId
                    && (currency.TenantId == type.TenantId || currency.TenantId == Guid.Empty)
                    && currency.IsEnabled && !currency.IsDeleted, ct))
            .When(type => type.CurrencyTypeId.HasValue)
            .WithMessage("Currency must be active and available to this tenant.");

        RuleFor(type => type.CurrencyTypeId)
            .MustAsync((type, _, ct) => CanChangeCurrencyAsync(dbContext, type, ct))
            .WithMessage("An established currency cannot be changed or cleared after the wallet type has been used.");
    }

    private static async Task<bool> CanChangeCurrencyAsync(DbContext dbContext, WalletType type, CancellationToken ct)
    {
        // Read persisted values, not the tracked entity already modified by the remote field patch.
        var establishedCurrencyId = await dbContext.Set<WalletType>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(existing => existing.Id == type.Id && existing.TenantId == type.TenantId)
            .Select(existing => existing.CurrencyTypeId)
            .FirstOrDefaultAsync(ct);
        if (establishedCurrencyId is null || establishedCurrencyId == type.CurrencyTypeId)
            return true;

        // Historical usage still fixes the denomination; all checks retain explicit tenant scope.
        return !await dbContext.Set<Wallet>().IgnoreQueryFilters()
                .AnyAsync(wallet => wallet.TenantId == type.TenantId && wallet.WalletTypeId == type.Id, ct)
            && !await dbContext.Set<WalletLedgerEntry>().IgnoreQueryFilters()
                .AnyAsync(entry => entry.TenantId == type.TenantId && entry.WalletTypeId == type.Id, ct)
            && !await dbContext.Set<DepositRequest>().IgnoreQueryFilters()
                .AnyAsync(request => request.TenantId == type.TenantId && request.WalletTypeId == type.Id, ct);
    }
}
