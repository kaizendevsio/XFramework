using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using XFramework.Domain.BO;

namespace XFramework.Core.DataAccess.Query.Entity.UserWallet
{
    public class GetUserWalletQuery : UserAuthBO, IRequest<List<bool>>
    {
    }
}
