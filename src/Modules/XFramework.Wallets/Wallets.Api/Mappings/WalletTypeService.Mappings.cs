using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Contracts;

public partial class WalletTypeService
{
    protected virtual partial WalletType MapCreateRequestToEntity(CreateWalletTypeRequest request)
    {
        return new WalletType
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            Name = request.Name,
            Desc = request.Desc,
            Type = request.Type,
            CurrencyTypeId = request.CurrencyTypeId,
            MinTransferRule = request.MinTransferRule,
            MaxTransferRule = request.MaxTransferRule,
            BondBalanceRule = request.BondBalanceRule,
            MaintainingBalanceRule = request.MaintainingBalanceRule,
            SystemReferenceId = request.SystemReferenceId,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    protected virtual partial void MapUpdateRequestToEntity(UpdateWalletTypeRequest request, WalletType entity)
    {
        entity.Code = request.Code;
        entity.Name = request.Name;
        entity.Desc = request.Desc;
        entity.Type = request.Type;
        entity.CurrencyTypeId = request.CurrencyTypeId;
        entity.MinTransferRule = request.MinTransferRule;
        entity.MaxTransferRule = request.MaxTransferRule;
        entity.BondBalanceRule = request.BondBalanceRule;
        entity.MaintainingBalanceRule = request.MaintainingBalanceRule;
        entity.ModifiedAt = DateTime.UtcNow;
    }

    protected virtual partial IQueryable<WalletType> ApplyFilters(IQueryable<WalletType> query, GetWalletTypeListRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchLower = request.SearchTerm.ToLower();
            query = query.Where(w =>
                w.Code.ToLower().Contains(searchLower) ||
                w.Name.ToLower().Contains(searchLower) ||
                (w.Desc != null && w.Desc.ToLower().Contains(searchLower)));
        }

        if (request.Type.HasValue)
        {
            query = query.Where(w => w.Type == request.Type.Value);
        }

        if (request.CurrencyTypeId.HasValue)
        {
            query = query.Where(w => w.CurrencyTypeId == request.CurrencyTypeId.Value);
        }

        return query;
    }
}
