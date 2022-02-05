using Wallets.Core.DataAccess.Commands.Entity;
using Wallets.Domain.Generic.Contracts.Requests.Get;

namespace Wallets.Core.DataAccess.Query.Entity.Wallets;

public class GetWalletEntityListQuery : GetWalletEntityListRequest, IRequest<QueryResponseBO<List<WalletEntityResponse>>>
{
}