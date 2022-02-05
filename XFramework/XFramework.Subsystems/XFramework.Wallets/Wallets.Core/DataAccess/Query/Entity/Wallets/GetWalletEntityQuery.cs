using Wallets.Core.DataAccess.Commands.Entity;
using Wallets.Domain.Generic.Contracts.Requests.Get;

namespace Wallets.Core.DataAccess.Query.Entity.Wallets;

public class GetWalletEntityQuery : GetWalletEntityRequest, IRequest<QueryResponseBO<WalletEntityResponse>>
{
}