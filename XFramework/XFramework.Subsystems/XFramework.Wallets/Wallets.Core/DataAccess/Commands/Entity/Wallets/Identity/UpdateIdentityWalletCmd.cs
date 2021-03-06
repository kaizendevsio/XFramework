﻿using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity
{
    public class UpdateIdentityWalletCmd : CommandBaseEntity, IRequest<CmdResponseBO<UpdateIdentityWalletCmd>>
    {
        public long UserAuthId { get; set; }
        public long? WalletTypeId { get; set; }
        public decimal? Balance { get; set; }
    }
}