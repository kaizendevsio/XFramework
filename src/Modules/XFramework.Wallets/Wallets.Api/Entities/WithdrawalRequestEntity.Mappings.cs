using XFramework.Domain.Shared.Contracts;

namespace XFramework.Wallets.Api.Entities;

/// <summary>
/// Partial implementation of WithdrawalRequestEntityService with mapping methods.
/// These methods provide the customization points for the auto-generated service.
/// </summary>
public partial class WithdrawalRequestEntityService
{
    /// <summary>
    /// Maps a CreateWithdrawalRequestEntityRequest to a new WithdrawalRequestEntity.
    /// </summary>
    protected virtual partial WithdrawalRequestEntity MapCreateRequestToEntity(CreateWithdrawalRequestEntityRequest request)
    {
        return new WithdrawalRequestEntity
        {
            Id = Guid.NewGuid(),
            CredentialId = request.CredentialId,
            Address = request.Address,
            Amount = request.Amount,
            Fee = request.Fee,
            WithdrawalStatus = request.WithdrawalStatus,
            Remarks = request.Remarks,
            ReferenceNumber = request.ReferenceNumber,
            WalletId = request.WalletId,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Maps an UpdateWithdrawalRequestEntityRequest to an existing WithdrawalRequestEntity.
    /// </summary>
    protected virtual partial void MapUpdateRequestToEntity(UpdateWithdrawalRequestEntityRequest request, WithdrawalRequestEntity entity)
    {
        entity.Address = request.Address;
        entity.Amount = request.Amount;
        entity.Fee = request.Fee;
        entity.WithdrawalStatus = request.WithdrawalStatus;
        entity.Remarks = request.Remarks;
        entity.ReferenceNumber = request.ReferenceNumber;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies filters to the query based on the request.
    /// </summary>
    protected virtual partial IQueryable<WithdrawalRequestEntity> ApplyFilters(IQueryable<WithdrawalRequestEntity> query, GetWithdrawalRequestEntityListRequest request)
    {
        if (request.CredentialId.HasValue)
        {
            query = query.Where(w => w.CredentialId == request.CredentialId.Value);
        }

        if (request.WalletId.HasValue)
        {
            query = query.Where(w => w.WalletId == request.WalletId.Value);
        }

        if (request.WithdrawalStatus.HasValue)
        {
            query = query.Where(w => w.WithdrawalStatus == request.WithdrawalStatus.Value);
        }

        if (request.StartDate.HasValue)
        {
            query = query.Where(w => w.CreatedAt >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            query = query.Where(w => w.CreatedAt <= request.EndDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchLower = request.SearchTerm.ToLower();
            query = query.Where(w =>
                (w.Address != null && w.Address.ToLower().Contains(searchLower)) ||
                (w.ReferenceNumber != null && w.ReferenceNumber.ToLower().Contains(searchLower)) ||
                (w.Remarks != null && w.Remarks.ToLower().Contains(searchLower)));
        }

        return query;
    }
}

/// <summary>
/// Domain mapping extensions for WithdrawalRequestEntity.
/// Implements bidirectional mapping between Domain.Shared.WithdrawalRequest and WithdrawalRequestEntity.
/// </summary>
public partial class WithdrawalRequestEntity
{
    /// <summary>
    /// Maps a Domain WithdrawalRequest to a WithdrawalRequestEntity (VSA wrapper).
    /// </summary>
    public static WithdrawalRequestEntity FromDomain(WithdrawalRequest domain)
    {
        return new WithdrawalRequestEntity
        {
            Id = domain.Id,
            CredentialId = domain.CredentialId,
            Address = domain.Address,
            Amount = domain.Amount,
            Fee = domain.Fee,
            WithdrawalStatus = domain.WithdrawalStatus,
            Remarks = domain.Remarks,
            ReferenceNumber = domain.ReferenceNumber,
            WalletId = domain.WalletId,
            IsDeleted = domain.IsDeleted,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.ModifiedAt
        };
    }

    /// <summary>
    /// Maps this WithdrawalRequestEntity (VSA wrapper) to a Domain WithdrawalRequest.
    /// </summary>
    public WithdrawalRequest ToDomain()
    {
        return new WithdrawalRequest
        {
            Id = this.Id,
            CredentialId = this.CredentialId,
            Address = this.Address,
            Amount = this.Amount,
            Fee = this.Fee,
            WithdrawalStatus = this.WithdrawalStatus,
            Remarks = this.Remarks,
            ReferenceNumber = this.ReferenceNumber,
            WalletId = this.WalletId,
            IsDeleted = this.IsDeleted,
            CreatedAt = this.CreatedAt,
            ModifiedAt = this.UpdatedAt
        };
    }
}