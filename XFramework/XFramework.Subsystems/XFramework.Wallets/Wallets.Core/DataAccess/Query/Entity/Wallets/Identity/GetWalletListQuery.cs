using Wallets.Domain.Generic.Contracts.Requests.Get;

namespace Wallets.Core.DataAccess.Query.Entity.Wallets.Identity;

public class GetWalletListQuery : GetWalletListRequest, IRequest<QueryResponseBO<List<WalletResponse>>>
{
}