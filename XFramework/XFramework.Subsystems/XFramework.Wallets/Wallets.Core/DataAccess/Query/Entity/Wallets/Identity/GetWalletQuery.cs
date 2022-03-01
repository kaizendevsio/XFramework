using Wallets.Domain.Generic.Contracts.Requests.Get;

namespace Wallets.Core.DataAccess.Query.Entity.Wallets.Identity;

public class GetWalletQuery : GetWalletRequest, IRequest<QueryResponse<WalletResponse>>
{
}