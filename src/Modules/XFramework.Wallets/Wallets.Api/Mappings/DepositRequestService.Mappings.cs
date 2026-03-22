using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Contracts;

public partial class DepositRequestService
{
    protected virtual partial DepositRequest MapCreateRequestToEntity(CreateDepositRequestRequest request)
    {
        return new DepositRequest
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

    protected virtual partial void MapUpdateRequestToEntity(UpdateDepositRequestRequest request, DepositRequest entity)
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
        entity.ModifiedAt = DateTime.UtcNow;
    }

    protected virtual partial IQueryable<DepositRequest> ApplyFilters(IQueryable<DepositRequest> query, GetDepositRequestListRequest request)
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
