using FluentValidation;

namespace Wallets.Api.Features.ReferenceData;

public sealed class ExchangeRateValidator : AbstractValidator<ExchangeRate>
{
    public ExchangeRateValidator(DbContext dbContext)
    {
        RuleFor(x => x.SourceCurrencyTypeId).NotEmpty().MustAsync(
            (rate, id, ct) => CurrencyExistsAsync(dbContext, id, rate.TenantId, ct))
            .WithMessage("Source currency must be active and available to this tenant.");
        RuleFor(x => x.TargetCurrencyTypeId).NotEmpty().NotEqual(x => x.SourceCurrencyTypeId).MustAsync(
            (rate, id, ct) => CurrencyExistsAsync(dbContext, id, rate.TenantId, ct))
            .WithMessage("Target currency must be active and available to this tenant.");
        RuleFor(x => x.Value).NotNull().GreaterThan(0);
        RuleFor(x => x.Fee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ExpiryDate).GreaterThan(x => x.EffectivityDate)
            .When(x => x.ExpiryDate.HasValue && x.EffectivityDate.HasValue);
    }

    private static Task<bool> CurrencyExistsAsync(DbContext dbContext, Guid id, Guid tenantId, CancellationToken ct) =>
        dbContext.Set<CurrencyType>().IgnoreQueryFilters().AnyAsync(
            currency => currency.Id == id
                && (currency.TenantId == tenantId || currency.TenantId == Guid.Empty)
                && currency.IsEnabled && !currency.IsDeleted, ct);
}
