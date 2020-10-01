using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using XFramework.Data.BO;

namespace XFramework.Core.DataAccess.Commands.Entity.UserWallet
{
    public class ConvertWalletTransactionCmd : WalletTransactionBO, IRequest<bool>
    {
        public string FromWalletCode { get; set; }
        public string ToWalletCode { get; set; }
    }
}
