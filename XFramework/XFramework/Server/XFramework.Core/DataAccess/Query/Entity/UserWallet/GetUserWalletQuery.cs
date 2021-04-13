using MediatR;
using System.Collections.Generic;

namespace XFramework.Core.DataAccess.Query.Entity.UserWallet
{
    public class GetUserWalletQuery : UserAuthBO, IRequest<List<bool>>
    {
    }
}
