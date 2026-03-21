using XFramework.Domain.Shared.Contracts;

namespace XFramework.Wallets.Api.Entities;

/// <summary>
/// Partial implementation of CurrencyTypeEntityService with mapping methods.
/// These methods provide the customization points for the auto-generated service.
/// </summary>
public partial class CurrencyTypeEntityService
{
    /// <summary>
    /// Maps a CreateCurrencyTypeEntityRequest to a new CurrencyTypeEntity.
    /// </summary>
    protected virtual partial CurrencyTypeEntity MapCreateRequestToEntity(CreateCurrencyTypeEntityRequest request)
    {
        return new CurrencyTypeEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            CurrencyIsoCode3 = request.CurrencyIsoCode3,
            Description = request.Description,
            Type = request.Type,
            SystemReferenceId = request.SystemReferenceId,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Maps an UpdateCurrencyTypeEntityRequest to an existing CurrencyTypeEntity.
    /// </summary>
    protected virtual partial void MapUpdateRequestToEntity(UpdateCurrencyTypeEntityRequest request, CurrencyTypeEntity entity)
    {
        entity.Name = request.Name;
        entity.CurrencyIsoCode3 = request.CurrencyIsoCode3;
        entity.Description = request.Description;
        entity.Type = request.Type;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies filters to the query based on the request.
    /// </summary>
    protected virtual partial IQueryable<CurrencyTypeEntity> ApplyFilters(IQueryable<CurrencyTypeEntity> query, GetCurrencyTypeEntityListRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchLower = request.SearchTerm.ToLower();
            query = query.Where(c =>
                (c.Name != null && c.Name.ToLower().Contains(searchLower)) ||
                (c.CurrencyIsoCode3 != null && c.CurrencyIsoCode3.ToLower().Contains(searchLower)) ||
                (c.Description != null && c.Description.ToLower().Contains(searchLower)));
        }

        if (request.Type.HasValue)
        {
            query = query.Where(c => c.Type == request.Type.Value);
        }

        return query;
    }
}

/// <summary>
/// Domain mapping extensions for CurrencyTypeEntity.
/// Implements bidirectional mapping between Domain.Shared.CurrencyType and CurrencyTypeEntity.
/// </summary>
public partial class CurrencyTypeEntity
{
    /// <summary>
    /// Maps a Domain CurrencyType to a CurrencyTypeEntity (VSA wrapper).
    /// </summary>
    public static CurrencyTypeEntity FromDomain(CurrencyType domain)
    {
        return new CurrencyTypeEntity
        {
            Id = domain.Id,
            Name = domain.Name,
            CurrencyIsoCode3 = domain.CurrencyIsoCode3,
            Description = domain.Description,
            Type = domain.Type,
            SystemReferenceId = domain.SystemReferenceId,
            IsDeleted = domain.IsDeleted,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.ModifiedAt
        };
    }

    /// <summary>
    /// Maps this CurrencyTypeEntity (VSA wrapper) to a Domain CurrencyType.
    /// </summary>
    public CurrencyType ToDomain()
    {
        return new CurrencyType
        {
            Id = this.Id,
            Name = this.Name,
            CurrencyIsoCode3 = this.CurrencyIsoCode3,
            Description = this.Description,
            Type = this.Type,
            SystemReferenceId = this.SystemReferenceId,
            IsDeleted = this.IsDeleted,
            CreatedAt = this.CreatedAt,
            ModifiedAt = this.UpdatedAt
        };
    }
}