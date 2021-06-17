using System;
using MediatR;
using RBS.Domain.BusinessObjects;

namespace RBS.Core.DataAccess.Commands.Entity.Wallet.User
{
    public class WalletTransferCmd : CommandBaseEntity, IRequest<CmdResponseBO<WalletTransferCmd>>
    {
        public Guid Uid { get; set; }
        public string UserName { get; set; }

        public decimal Amount { get; set; }
        public int SourceWalletId { get; set; }
        public int TargetWalletId { get; set; }
    }
}