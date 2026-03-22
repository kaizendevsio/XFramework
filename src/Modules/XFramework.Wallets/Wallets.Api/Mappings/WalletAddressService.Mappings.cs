using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Contracts;

public partial class WalletAddressService
{
    protected virtual partial WalletAddress MapCreateRequestToEntity(CreateWalletAddressRequest request)
    {
        return new WalletAddress
        {
            Id = Guid.NewGuid(),
            Address = request.Address,
            Balance = request.Balance,
            Remarks = request.Remarks,
            WalletId = request.WalletId,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    protected virtual partial void MapUpdateRequestToEntity(UpdateWalletAddressRequest request, WalletAddress entity)
    {
        entity.Address = request.Address;
        entity.Balance = request.Balance;
        entity.Remarks = request.Remarks;
        entity.ModifiedAt = DateTime.UtcNow;
    }

    protected virtual partial IQueryable<WalletAddress> ApplyFilters(IQueryable<WalletAddress> query, GetWalletAddressListRequest request)
    {
        if (request.WalletId.HasValue)
        {
            query = query.Where(w => w.WalletId == request.WalletId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchLower = request.SearchTerm.ToLower();
            query = query.Where(w =>
                (w.Address != null && w.Address.ToLower().Contains(searchLower)) ||
                (w.Remarks != null && w.Remarks.ToLower().Contains(searchLower)));
        }

        return query;
    }
}
