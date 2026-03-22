using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Contracts;

public partial class ExchangeRateService
{
    protected virtual partial ExchangeRate MapCreateRequestToEntity(CreateExchangeRateRequest request)
    {
        return new ExchangeRate
        {
            Id = Guid.NewGuid(),
            SourceCurrencyTypeId = request.SourceCurrencyTypeId,
            TargetCurrencyTypeId = request.TargetCurrencyTypeId,
            Value = request.Value,
            Fee = request.Fee,
            EffectivityDate = request.EffectivityDate,
            ExpiryDate = request.ExpiryDate,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    protected virtual partial void MapUpdateRequestToEntity(UpdateExchangeRateRequest request, ExchangeRate entity)
    {
        entity.Value = request.Value;
        entity.Fee = request.Fee;
        entity.EffectivityDate = request.EffectivityDate;
        entity.ExpiryDate = request.ExpiryDate;
        entity.ModifiedAt = DateTime.UtcNow;
    }

    protected virtual partial IQueryable<ExchangeRate> ApplyFilters(IQueryable<ExchangeRate> query, GetExchangeRateListRequest request)
    {
        if (request.SourceCurrencyTypeId.HasValue)
        {
            query = query.Where(e => e.SourceCurrencyTypeId == request.SourceCurrencyTypeId.Value);
        }

        if (request.TargetCurrencyTypeId.HasValue)
        {
            query = query.Where(e => e.TargetCurrencyTypeId == request.TargetCurrencyTypeId.Value);
        }

        if (request.EffectiveOn.HasValue)
        {
            query = query.Where(e =>
                (e.EffectivityDate == null || e.EffectivityDate <= request.EffectiveOn.Value) &&
                (e.ExpiryDate == null || e.ExpiryDate >= request.EffectiveOn.Value));
        }

        return query;
    }
}
