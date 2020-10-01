using System;
using System.Collections.Generic;
using System.Text;
using XFramework.Core.DataAccess.Commands.Entity.UserWallet;
using XFramework.Core.DataAccess.Query.Entity.UserWallet;

namespace XFramework.Core.Interfaces.Commands
{
    public interface IUserWalletCommandHandler
    {
        public bool Handle(CreateWalletTransactionCmd command);
        public bool Handle(ConvertWalletTransactionCmd command);
    }
}
