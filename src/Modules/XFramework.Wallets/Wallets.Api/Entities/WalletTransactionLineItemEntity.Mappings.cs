using XFramework.Domain.Shared.Contracts;

namespace XFramework.Wallets.Api.Entities;

/// <summary>
/// Partial implementation of WalletTransactionLineItemEntityService with mapping methods.
/// These methods provide the customization points for the auto-generated service.
/// </summary>
public partial class WalletTransactionLineItemEntityService
{
    /// <summary>
    /// Maps a CreateWalletTransactionLineItemEntityRequest to a new WalletTransactionLineItemEntity.
    /// </summary>
    protected virtual partial WalletTransactionLineItemEntity MapCreateRequestToEntity(CreateWalletTransactionLineItemEntityRequest request)
    {
        return new WalletTransactionLineItemEntity
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

    /// <summary>
    /// Maps an UpdateWalletTransactionLineItemEntityRequest to an existing WalletTransactionLineItemEntity.
    /// </summary>
    protected virtual partial void MapUpdateRequestToEntity(UpdateWalletTransactionLineItemEntityRequest request, WalletTransactionLineItemEntity entity)
    {
        entity.Amount = request.Amount;
        entity.Fee = request.Fee;
        entity.Description = request.Description;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies filters to the query based on the request.
    /// </summary>
    protected virtual partial IQueryable<WalletTransactionLineItemEntity> ApplyFilters(IQueryable<WalletTransactionLineItemEntity> query, GetWalletTransactionLineItemEntityListRequest request)
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

/// <summary>
/// Domain mapping extensions for WalletTransactionLineItemEntity.
/// Implements bidirectional mapping between Domain.Shared.WalletTransactionLineItem and WalletTransactionLineItemEntity.
/// </summary>
public partial class WalletTransactionLineItemEntity
{
    /// <summary>
    /// Maps a Domain WalletTransactionLineItem to a WalletTransactionLineItemEntity (VSA wrapper).
    /// </summary>
    public static WalletTransactionLineItemEntity FromDomain(WalletTransactionLineItem domain)
    {
        return new WalletTransactionLineItemEntity
        {
            Id = domain.Id,
            Amount = domain.Amount,
            Fee = domain.Fee,
            Description = domain.Description,
            WalletTransferId = domain.WalletTransferId,
            IsDeleted = domain.IsDeleted,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.ModifiedAt
        };
    }

    /// <summary>
    /// Maps this WalletTransactionLineItemEntity (VSA wrapper) to a Domain WalletTransactionLineItem.
    /// </summary>
    public WalletTransactionLineItem ToDomain()
    {
        return new WalletTransactionLineItem
        {
            Id = this.Id,
            Amount = this.Amount,
            Fee = this.Fee,
            Description = this.Description,
            WalletTransferId = this.WalletTransferId,
            IsDeleted = this.IsDeleted,
            CreatedAt = this.CreatedAt,
            ModifiedAt = this.UpdatedAt
        };
    }
}