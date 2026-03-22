using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Contracts;

public partial class WalletTransactionLineItemService
{
    protected virtual partial WalletTransactionLineItem MapCreateRequestToEntity(CreateWalletTransactionLineItemRequest request)
    {
        return new WalletTransactionLineItem
        {
            Id = Guid.NewGuid(),
            Amount = request.Amount,
            Fee = request.Fee,
            Description = request.Description,
            WalletTransferId = request.WalletTransferId,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    protected virtual partial void MapUpdateRequestToEntity(UpdateWalletTransactionLineItemRequest request, WalletTransactionLineItem entity)
    {
        entity.Amount = request.Amount;
        entity.Fee = request.Fee;
        entity.Description = request.Description;
        entity.ModifiedAt = DateTime.UtcNow;
    }

    protected virtual partial IQueryable<WalletTransactionLineItem> ApplyFilters(IQueryable<WalletTransactionLineItem> query, GetWalletTransactionLineItemListRequest request)
    {
        if (request.WalletTransferId.HasValue)
        {
            query = query.Where(l => l.WalletTransferId == request.WalletTransferId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchLower = request.SearchTerm.ToLower();
            query = query.Where(l =>
                l.Description != null && l.Description.ToLower().Contains(searchLower));
        }

        return query;
    }
}
