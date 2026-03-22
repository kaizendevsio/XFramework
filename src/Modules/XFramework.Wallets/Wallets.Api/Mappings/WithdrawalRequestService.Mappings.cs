using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Contracts;

public partial class WithdrawalRequestService
{
    protected virtual partial WithdrawalRequest MapCreateRequestToEntity(CreateWithdrawalRequestRequest request)
    {
        return new WithdrawalRequest
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

    protected virtual partial void MapUpdateRequestToEntity(UpdateWithdrawalRequestRequest request, WithdrawalRequest entity)
    {
        entity.Address = request.Address;
        entity.Amount = request.Amount;
        entity.Fee = request.Fee;
        entity.WithdrawalStatus = request.WithdrawalStatus;
        entity.Remarks = request.Remarks;
        entity.ReferenceNumber = request.ReferenceNumber;
        entity.ModifiedAt = DateTime.UtcNow;
    }

    protected virtual partial IQueryable<WithdrawalRequest> ApplyFilters(IQueryable<WithdrawalRequest> query, GetWithdrawalRequestListRequest request)
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
