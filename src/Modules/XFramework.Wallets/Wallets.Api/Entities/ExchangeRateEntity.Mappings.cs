using XFramework.Domain.Shared.Contracts;

namespace XFramework.Wallets.Api.Entities;

/// <summary>
/// Partial implementation of ExchangeRateEntityService with mapping methods.
/// These methods provide the customization points for the auto-generated service.
/// </summary>
public partial class ExchangeRateEntityService
{
    /// <summary>
    /// Maps a CreateExchangeRateEntityRequest to a new ExchangeRateEntity.
    /// </summary>
    protected virtual partial ExchangeRateEntity MapCreateRequestToEntity(CreateExchangeRateEntityRequest request)
    {
        return new ExchangeRateEntity
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

    /// <summary>
    /// Maps an UpdateExchangeRateEntityRequest to an existing ExchangeRateEntity.
    /// </summary>
    protected virtual partial void MapUpdateRequestToEntity(UpdateExchangeRateEntityRequest request, ExchangeRateEntity entity)
    {
        entity.Value = request.Value;
        entity.Fee = request.Fee;
        entity.EffectivityDate = request.EffectivityDate;
        entity.ExpiryDate = request.ExpiryDate;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies filters to the query based on the request.
    /// </summary>
    protected virtual partial IQueryable<ExchangeRateEntity> ApplyFilters(IQueryable<ExchangeRateEntity> query, GetExchangeRateEntityListRequest request)
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

/// <summary>
/// Domain mapping extensions for ExchangeRateEntity.
/// Implements bidirectional mapping between Domain.Shared.ExchangeRate and ExchangeRateEntity.
/// </summary>
public partial class ExchangeRateEntity
{
    /// <summary>
    /// Maps a Domain ExchangeRate to an ExchangeRateEntity (VSA wrapper).
    /// </summary>
    public static ExchangeRateEntity FromDomain(ExchangeRate domain)
    {
        return new ExchangeRateEntity
        {
            Id = domain.Id,
            SourceCurrencyTypeId = domain.SourceCurrencyTypeId,
            TargetCurrencyTypeId = domain.TargetCurrencyTypeId,
            Value = domain.Value,
            Fee = domain.Fee,
            EffectivityDate = domain.EffectivityDate,
            ExpiryDate = domain.ExpiryDate,
            IsDeleted = domain.IsDeleted,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.ModifiedAt
        };
    }

    /// <summary>
    /// Maps this ExchangeRateEntity (VSA wrapper) to a Domain ExchangeRate.
    /// </summary>
    public ExchangeRate ToDomain()
    {
        return new ExchangeRate
        {
            Id = this.Id,
            SourceCurrencyTypeId = this.SourceCurrencyTypeId,
            TargetCurrencyTypeId = this.TargetCurrencyTypeId,
            Value = this.Value,
            Fee = this.Fee,
            EffectivityDate = this.EffectivityDate,
            ExpiryDate = this.ExpiryDate,
            IsDeleted = this.IsDeleted,
            CreatedAt = this.CreatedAt,
            ModifiedAt = this.UpdatedAt
        };
    }
}