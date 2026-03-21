using XFramework.Domain.Shared.Contracts;

namespace XFramework.Wallets.Api.Entities;

/// <summary>
/// Partial implementation of WalletAddressEntityService with mapping methods.
/// These methods provide the customization points for the auto-generated service.
/// </summary>
public partial class WalletAddressEntityService
{
    /// <summary>
    /// Maps a CreateWalletAddressEntityRequest to a new WalletAddressEntity.
    /// </summary>
    protected virtual partial WalletAddressEntity MapCreateRequestToEntity(CreateWalletAddressEntityRequest request)
    {
        return new WalletAddressEntity
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

    /// <summary>
    /// Maps an UpdateWalletAddressEntityRequest to an existing WalletAddressEntity.
    /// </summary>
    protected virtual partial void MapUpdateRequestToEntity(UpdateWalletAddressEntityRequest request, WalletAddressEntity entity)
    {
        entity.Address = request.Address;
        entity.Balance = request.Balance;
        entity.Remarks = request.Remarks;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies filters to the query based on the request.
    /// </summary>
    protected virtual partial IQueryable<WalletAddressEntity> ApplyFilters(IQueryable<WalletAddressEntity> query, GetWalletAddressEntityListRequest request)
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

/// <summary>
/// Domain mapping extensions for WalletAddressEntity.
/// Implements bidirectional mapping between Domain.Shared.WalletAddress and WalletAddressEntity.
/// </summary>
public partial class WalletAddressEntity
{
    /// <summary>
    /// Maps a Domain WalletAddress to a WalletAddressEntity (VSA wrapper).
    /// </summary>
    public static WalletAddressEntity FromDomain(WalletAddress domain)
    {
        return new WalletAddressEntity
        {
            Id = domain.Id,
            Address = domain.Address,
            Balance = domain.Balance,
            Remarks = domain.Remarks,
            WalletId = domain.WalletId,
            IsDeleted = domain.IsDeleted,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.ModifiedAt
        };
    }

    /// <summary>
    /// Maps this WalletAddressEntity (VSA wrapper) to a Domain WalletAddress.
    /// </summary>
    public WalletAddress ToDomain()
    {
        return new WalletAddress
        {
            Id = this.Id,
            Address = this.Address,
            Balance = this.Balance,
            Remarks = this.Remarks,
            WalletId = this.WalletId,
            IsDeleted = this.IsDeleted,
            CreatedAt = this.CreatedAt,
            ModifiedAt = this.UpdatedAt
        };
    }
}