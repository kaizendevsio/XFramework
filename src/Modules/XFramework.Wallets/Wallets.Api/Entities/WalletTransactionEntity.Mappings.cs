using XFramework.Domain.Shared.Contracts;

namespace XFramework.Wallets.Api.Entities;

/// <summary>
/// Partial implementation of WalletTransactionEntityService with mapping methods.
/// These methods provide the customization points for the auto-generated service.
/// Note: WalletTransactionEntity is read-only (Get | GetList only). Use IWalletService for mutations.
/// </summary>
public partial class WalletTransactionEntityService
{
    /// <summary>
    /// Applies filters to the query based on the request.
    /// </summary>
    protected virtual partial IQueryable<WalletTransactionEntity> ApplyFilters(IQueryable<WalletTransactionEntity> query, GetWalletTransactionEntityListRequest request)
    {
        if (request.CredentialId.HasValue)
        {
            query = query.Where(t => t.CredentialId == request.CredentialId.Value);
        }

        if (request.WalletId.HasValue)
        {
            query = query.Where(t => t.WalletId == request.WalletId.Value);
        }

        if (request.TransactionType.HasValue)
        {
            query = query.Where(t => t.TransactionType == request.TransactionType.Value);
        }

        if (request.Held.HasValue)
        {
            query = query.Where(t => t.Held == request.Held.Value);
        }

        if (request.Released.HasValue)
        {
            query = query.Where(t => t.Released == request.Released.Value);
        }

        if (request.StartDate.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            query = query.Where(t => t.CreatedAt <= request.EndDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchLower = request.SearchTerm.ToLower();
            query = query.Where(t =>
                (t.ReferenceNumber != null && t.ReferenceNumber.ToLower().Contains(searchLower)) ||
                (t.Description != null && t.Description.ToLower().Contains(searchLower)) ||
                (t.Remarks != null && t.Remarks.ToLower().Contains(searchLower)));
        }

        return query;
    }
}

/// <summary>
/// Domain mapping extensions for WalletTransactionEntity.
/// Implements bidirectional mapping between Domain.Shared.WalletTransaction and WalletTransactionEntity.
/// </summary>
public partial class WalletTransactionEntity
{
    /// <summary>
    /// Maps a Domain WalletTransaction to a WalletTransactionEntity (VSA wrapper).
    /// Stores computed values (Amount, NetAmount, TotalFees) for read operations.
    /// </summary>
    public static WalletTransactionEntity FromDomain(WalletTransaction domain)
    {
        return new WalletTransactionEntity
        {
            Id = domain.Id,
            CredentialId = domain.CredentialId,
            WalletId = domain.WalletId,
            // Store computed Amount value
            Amount = domain.Amount,
            // Store computed NetAmount value
            NetAmount = domain.NetAmount,
            Held = domain.Held,
            Released = domain.Released,
            Remarks = domain.Remarks,
            TransactionFee = domain.TransactionFee,
            // Store computed TotalFees value
            TotalFees = domain.TotalFees,
            ReferenceNumber = domain.ReferenceNumber,
            Description = domain.Description,
            RunningTotalBalance = domain.RunningTotalBalance,
            RunningAvailableBalance = domain.RunningAvailableBalance,
            RunningBalance = domain.RunningBalance,
            RunningDebitOnHoldBalance = domain.RunningDebitOnHoldBalance,
            RunningCreditOnHoldBalance = domain.RunningCreditOnHoldBalance,
            PreviousTotalBalance = domain.PreviousTotalBalance,
            PreviousBalance = domain.PreviousBalance,
            PreviousDebitOnHoldBalance = domain.PreviousDebitOnHoldBalance,
            PreviousCreditOnHoldBalance = domain.PreviousCreditOnHoldBalance,
            TransactionType = domain.TransactionType,
            IsDeleted = domain.IsDeleted,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.ModifiedAt
        };
    }

    /// <summary>
    /// Maps this WalletTransactionEntity (VSA wrapper) to a Domain WalletTransaction.
    /// Note: The domain model will recalculate computed properties from InternalAmount.
    /// </summary>
    public WalletTransaction ToDomain()
    {
        return new WalletTransaction
        {
            Id = this.Id,
            CredentialId = this.CredentialId,
            WalletId = this.WalletId,
            // Set Amount - domain will handle InternalAmount
            Amount = this.Amount,
            Held = this.Held,
            Released = this.Released,
            Remarks = this.Remarks,
            TransactionFee = this.TransactionFee,
            ReferenceNumber = this.ReferenceNumber,
            Description = this.Description,
            RunningTotalBalance = this.RunningTotalBalance,
            RunningAvailableBalance = this.RunningAvailableBalance,
            RunningBalance = this.RunningBalance,
            RunningDebitOnHoldBalance = this.RunningDebitOnHoldBalance,
            RunningCreditOnHoldBalance = this.RunningCreditOnHoldBalance,
            PreviousTotalBalance = this.PreviousTotalBalance,
            PreviousBalance = this.PreviousBalance,
            PreviousDebitOnHoldBalance = this.PreviousDebitOnHoldBalance,
            PreviousCreditOnHoldBalance = this.PreviousCreditOnHoldBalance,
            TransactionType = this.TransactionType,
            IsDeleted = this.IsDeleted,
            CreatedAt = this.CreatedAt,
            ModifiedAt = this.UpdatedAt
        };
    }
}