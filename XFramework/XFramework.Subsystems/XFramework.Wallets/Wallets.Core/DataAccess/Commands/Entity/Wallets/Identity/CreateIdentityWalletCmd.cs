using System;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity
{
    public class CreateIdentityWalletCmd : CommandBaseEntity, IRequest<CmdResponseBO<CreateIdentityWalletCmd>>
    {
        public long UserAuthId { get; set; }
        public long? WalletTypeId { get; set; }
        public decimal? Balance { get; set; }
    }
}