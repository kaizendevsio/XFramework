using XFramework.Domain.Shared.Contracts;

namespace XFramework.Wallets.Api.Entities;

/// <summary>
/// Partial implementation of WalletTransferEntityService with mapping methods.
/// These methods provide the customization points for the auto-generated service.
/// Note: WalletTransferEntity is read-only (Get | GetList only). Use IWalletService for mutations.
/// </summary>
public partial class WalletTransferEntityService
{
    /// <summary>
    /// Applies filters to the query based on the request.
    /// </summary>
    protected virtual partial IQueryable<WalletTransferEntity> ApplyFilters(IQueryable<WalletTransferEntity> query, GetWalletTransferEntityListRequest request)
    {
        if (request.SenderTransactionId.HasValue)
        {
            query = query.Where(t => t.SenderTransactionId == request.SenderTransactionId.Value);
        }

        if (request.RecipientTransactionId.HasValue)
        {
            query = query.Where(t => t.RecipientTransactionId == request.RecipientTransactionId.Value);
        }

        if (request.TransactionPurpose.HasValue)
        {
            query = query.Where(t => t.TransactionPurpose == request.TransactionPurpose.Value);
        }

        if (request.StartDate.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            query = query.Where(t => t.CreatedAt <= request.EndDate.Value);
        }

        return query;
    }
}

/// <summary>
/// Domain mapping extensions for WalletTransferEntity.
/// Implements bidirectional mapping between Domain.Shared.WalletTransfer and WalletTransferEntity.
/// </summary>
public partial class WalletTransferEntity
{
    /// <summary>
    /// Maps a Domain WalletTransfer to a WalletTransferEntity (VSA wrapper).
    /// Stores computed values (Amount, TotalFees) for read operations.
    /// </summary>
    public static WalletTransferEntity FromDomain(WalletTransfer domain)
    {
        return new WalletTransferEntity
        {
            Id = domain.Id,
            TransactionPurpose = domain.TransactionPurpose,
            SenderTransactionId = domain.SenderTransactionId,
            RecipientTransactionId = domain.RecipientTransactionId,
            // Store computed Amount value
            Amount = domain.Amount,
            TransactionFee = domain.TransactionFee,
            // Store computed TotalFees value
            TotalFees = domain.TotalFees,
            IsDeleted = domain.IsDeleted,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.ModifiedAt
        };
    }

    /// <summary>
    /// Maps this WalletTransferEntity (VSA wrapper) to a Domain WalletTransfer.
    /// Note: The domain model will recalculate computed properties from LineItems.
    /// </summary>
    public WalletTransfer ToDomain()
    {
        return new WalletTransfer
        {
            Id = this.Id,
            TransactionPurpose = this.TransactionPurpose,
            SenderTransactionId = this.SenderTransactionId,
            RecipientTransactionId = this.RecipientTransactionId,
            TransactionFee = this.TransactionFee,
            // Amount and TotalFees are computed in domain from LineItems
            IsDeleted = this.IsDeleted,
            CreatedAt = this.CreatedAt,
            ModifiedAt = this.UpdatedAt
        };
    }
}