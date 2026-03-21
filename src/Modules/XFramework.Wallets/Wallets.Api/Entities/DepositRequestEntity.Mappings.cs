using XFramework.Domain.Shared.Contracts;

namespace XFramework.Wallets.Api.Entities;

/// <summary>
/// Partial implementation of DepositRequestEntityService with mapping methods.
/// These methods provide the customization points for the auto-generated service.
/// </summary>
public partial class DepositRequestEntityService
{
    /// <summary>
    /// Maps a CreateDepositRequestEntityRequest to a new DepositRequestEntity.
    /// </summary>
    protected virtual partial DepositRequestEntity MapCreateRequestToEntity(CreateDepositRequestEntityRequest request)
    {
        return new DepositRequestEntity
        {
            Id = Guid.NewGuid(),
            CredentialId = request.CredentialId,
            SourceCurrencyId = request.SourceCurrencyId,
            WalletTypeId = request.WalletTypeId,
            Address = request.Address,
            Amount = request.Amount,
            Remarks = request.Remarks,
            DepositStatus = request.DepositStatus,
            ExpiryDate = request.ExpiryDate,
            RawRequestData = request.RawRequestData,
            ReferenceNo = request.ReferenceNo,
            Discount = request.Discount,
            ConvenienceFee = request.ConvenienceFee,
            SystemFee = request.SystemFee,
            DiscountType = request.DiscountType,
            GatewayId = request.GatewayId,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Maps an UpdateDepositRequestEntityRequest to an existing DepositRequestEntity.
    /// </summary>
    protected virtual partial void MapUpdateRequestToEntity(UpdateDepositRequestEntityRequest request, DepositRequestEntity entity)
    {
        entity.Address = request.Address;
        entity.Amount = request.Amount;
        entity.Remarks = request.Remarks;
        entity.DepositStatus = request.DepositStatus;
        entity.ExpiryDate = request.ExpiryDate;
        entity.RawResponseData = request.RawResponseData;
        entity.Discount = request.Discount;
        entity.ConvenienceFee = request.ConvenienceFee;
        entity.SystemFee = request.SystemFee;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies filters to the query based on the request.
    /// </summary>
    protected virtual partial IQueryable<DepositRequestEntity> ApplyFilters(IQueryable<DepositRequestEntity> query, GetDepositRequestEntityListRequest request)
    {
        if (request.CredentialId.HasValue)
        {
            query = query.Where(d => d.CredentialId == request.CredentialId.Value);
        }

        if (request.WalletTypeId.HasValue)
        {
            query = query.Where(d => d.WalletTypeId == request.WalletTypeId.Value);
        }

        if (request.SourceCurrencyId.HasValue)
        {
            query = query.Where(d => d.SourceCurrencyId == request.SourceCurrencyId.Value);
        }

        if (request.DepositStatus.HasValue)
        {
            query = query.Where(d => d.DepositStatus == request.DepositStatus.Value);
        }

        if (request.StartDate.HasValue)
        {
            query = query.Where(d => d.CreatedAt >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            query = query.Where(d => d.CreatedAt <= request.EndDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchLower = request.SearchTerm.ToLower();
            query = query.Where(d =>
                (d.Address != null && d.Address.ToLower().Contains(searchLower)) ||
                (d.ReferenceNo != null && d.ReferenceNo.ToLower().Contains(searchLower)) ||
                (d.Remarks != null && d.Remarks.ToLower().Contains(searchLower)));
        }

        return query;
    }
}

/// <summary>
/// Domain mapping extensions for DepositRequestEntity.
/// Implements bidirectional mapping between Domain.Shared.DepositRequest and DepositRequestEntity.
/// </summary>
public partial class DepositRequestEntity
{
    /// <summary>
    /// Maps a Domain DepositRequest to a DepositRequestEntity (VSA wrapper).
    /// </summary>
    public static DepositRequestEntity FromDomain(DepositRequest domain)
    {
        return new DepositRequestEntity
        {
            Id = domain.Id,
            CredentialId = domain.CredentialId,
            SourceCurrencyId = domain.SourceCurrencyId,
            WalletTypeId = domain.WalletTypeId,
            Address = domain.Address,
            Amount = domain.Amount,
            Remarks = domain.Remarks,
            DepositStatus = domain.DepositStatus,
            ExpiryDate = domain.ExpiryDate,
            RawRequestData = domain.RawRequestData,
            ReferenceNo = domain.ReferenceNo,
            RawResponseData = domain.RawResponseData,
            Discount = domain.Discount,
            ConvenienceFee = domain.ConvenienceFee,
            SystemFee = domain.SystemFee,
            DiscountType = domain.DiscountType,
            GatewayId = domain.GatewayId,
            IsDeleted = domain.IsDeleted,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.ModifiedAt
        };
    }

    /// <summary>
    /// Maps this DepositRequestEntity (VSA wrapper) to a Domain DepositRequest.
    /// </summary>
    public DepositRequest ToDomain()
    {
        return new DepositRequest
        {
            Id = this.Id,
            CredentialId = this.CredentialId,
            SourceCurrencyId = this.SourceCurrencyId,
            WalletTypeId = this.WalletTypeId,
            Address = this.Address,
            Amount = this.Amount,
            Remarks = this.Remarks,
            DepositStatus = this.DepositStatus,
            ExpiryDate = this.ExpiryDate,
            RawRequestData = this.RawRequestData,
            ReferenceNo = this.ReferenceNo,
            RawResponseData = this.RawResponseData,
            Discount = this.Discount,
            ConvenienceFee = this.ConvenienceFee,
            SystemFee = this.SystemFee,
            DiscountType = this.DiscountType,
            GatewayId = this.GatewayId,
            IsDeleted = this.IsDeleted,
            CreatedAt = this.CreatedAt,
            ModifiedAt = this.UpdatedAt
        };
    }
}