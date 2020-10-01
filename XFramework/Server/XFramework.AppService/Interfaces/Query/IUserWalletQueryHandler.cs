using System;
using System.Collections.Generic;
using System.Text;
using XFramework.Core.DataAccess.Query.Entity.UserWallet;

namespace XFramework.Core.Interfaces.Query
{
    public interface IUserWalletQueryHandler
    {
        public bool Handle(GetUserWalletQuery query);
        public bool Handle(GetUserWalletTransactionQuery query);
    }
}
