using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Contracts;

public partial class CurrencyTypeService
{
    protected virtual partial CurrencyType MapCreateRequestToEntity(CreateCurrencyTypeRequest request)
    {
        return new CurrencyType
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

    protected virtual partial void MapUpdateRequestToEntity(UpdateCurrencyTypeRequest request, CurrencyType entity)
    {
        entity.Name = request.Name;
        entity.CurrencyIsoCode3 = request.CurrencyIsoCode3;
        entity.Description = request.Description;
        entity.Type = request.Type;
        entity.ModifiedAt = DateTime.UtcNow;
    }

    protected virtual partial IQueryable<CurrencyType> ApplyFilters(IQueryable<CurrencyType> query, GetCurrencyTypeListRequest request)
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
