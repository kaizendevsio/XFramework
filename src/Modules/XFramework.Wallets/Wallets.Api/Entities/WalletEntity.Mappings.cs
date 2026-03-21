using XFramework.Domain.Shared.Contracts;

namespace XFramework.Wallets.Api.Entities;

/// <summary>
/// Partial implementation of WalletEntityService with mapping methods.
/// These methods provide the customization points for the auto-generated service.
/// Note: WalletEntity is read-only (Get | GetList only). Use IWalletService for mutations.
/// </summary>
public partial class WalletEntityService
{
    /// <summary>
    /// Applies filters to the query based on the request.
    /// </summary>
    protected virtual partial IQueryable<WalletEntity> ApplyFilters(IQueryable<WalletEntity> query, GetWalletEntityListRequest request)
    {
        if (request.CredentialId.HasValue)
        {
            query = query.Where(w => w.CredentialId == request.CredentialId.Value);
        }

        if (request.WalletTypeId.HasValue)
        {
            query = query.Where(w => w.WalletTypeId == request.WalletTypeId.Value);
        }

        if (request.MinBalance.HasValue)
        {
            query = query.Where(w => w.Balance >= request.MinBalance.Value);
        }

        if (request.MaxBalance.HasValue)
        {
            query = query.Where(w => w.Balance <= request.MaxBalance.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchLower = request.SearchTerm.ToLower();
            query = query.Where(w =>
                w.AccountNumber != null && w.AccountNumber.ToLower().Contains(searchLower));
        }

        return query;
    }
}

/// <summary>
/// Domain mapping extensions for WalletEntity.
/// Implements bidirectional mapping between Domain.Shared.Wallet and WalletEntity.
/// </summary>
public partial class WalletEntity
{
    /// <summary>
    /// Maps a Domain Wallet to a WalletEntity (VSA wrapper).
    /// Computes TotalBalance and AvailableBalance for read operations.
    /// </summary>
    public static WalletEntity FromDomain(Wallet domain)
    {
        return new WalletEntity
        {
            Id = domain.Id,
            CredentialId = domain.CredentialId,
            WalletTypeId = domain.WalletTypeId,
            Balance = domain.Balance,
            AccountNumber = domain.AccountNumber,
            CardNumber = domain.CardNumber,
            DebitOnHoldBalance = domain.DebitOnHoldBalance,
            CreditOnHoldBalance = domain.CreditOnHoldBalance,
            TransferableBalance = domain.TransferableBalance,
            MinTransferRule = domain.MinTransferRule,
            MaxTransferRule = domain.MaxTransferRule,
            BondBalanceRule = domain.BondBalanceRule,
            MaintainingBalanceRule = domain.MaintainingBalanceRule,
            // Compute the derived properties
            TotalBalance = domain.TotalBalance,
            AvailableBalance = domain.AvailableBalance,
            IsDeleted = domain.IsDeleted,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.ModifiedAt
        };
    }

    /// <summary>
    /// Maps this WalletEntity (VSA wrapper) to a Domain Wallet.
    /// Note: Computed properties are recalculated by the domain model.
    /// </summary>
    public Wallet ToDomain()
    {
        return new Wallet
        {
            Id = this.Id,
            CredentialId = this.CredentialId,
            WalletTypeId = this.WalletTypeId,
            Balance = this.Balance,
            AccountNumber = this.AccountNumber,
            CardNumber = this.CardNumber,
            DebitOnHoldBalance = this.DebitOnHoldBalance,
            CreditOnHoldBalance = this.CreditOnHoldBalance,
            TransferableBalance = this.TransferableBalance,
            MinTransferRule = this.MinTransferRule,
            MaxTransferRule = this.MaxTransferRule,
            BondBalanceRule = this.BondBalanceRule,
            MaintainingBalanceRule = this.MaintainingBalanceRule,
            // TotalBalance and AvailableBalance are computed properties in domain
            IsDeleted = this.IsDeleted,
            CreatedAt = this.CreatedAt,
            ModifiedAt = this.UpdatedAt
        };
    }
}