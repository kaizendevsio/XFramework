using Wallets.Domain.Shared.Contracts;

namespace Wallets.Domain.Shared.Contracts;

public partial class WalletTransferService
{
    protected virtual partial IQueryable<WalletTransfer> ApplyFilters(IQueryable<WalletTransfer> query, GetWalletTransferListRequest request)
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
