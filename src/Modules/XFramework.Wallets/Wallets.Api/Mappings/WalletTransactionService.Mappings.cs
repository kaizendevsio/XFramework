using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Contracts;

public partial class WalletTransactionService
{
    protected virtual partial IQueryable<WalletTransaction> ApplyFilters(IQueryable<WalletTransaction> query, GetWalletTransactionListRequest request)
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
