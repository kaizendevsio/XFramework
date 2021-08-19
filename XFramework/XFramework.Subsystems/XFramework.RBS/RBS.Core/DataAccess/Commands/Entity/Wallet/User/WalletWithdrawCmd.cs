using System;
using MediatR;
using RBS.Domain.BusinessObjects;

namespace RBS.Core.DataAccess.Commands.Entity.Wallet.User
{
    public class WalletWithdrawCmd : CommandBaseEntity, IRequest<CmdResponseBO<WalletWithdrawCmd>>
    {
        public Guid Uid { get; set; }
        public string UserName { get; set; }

        public decimal Amount { get; set; }
        public int WalletId { get; set; }
    }
}