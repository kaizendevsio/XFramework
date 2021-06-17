using System;
using MediatR;
using RBS.Core.DataAccess.Commands.Entity;
using RBS.Core.DataAccess.Commands.Entity.Wallet.User;
using RBS.Domain.BusinessObjects;
using RBS.Domain.Contracts.Responses;

namespace RBS.Core.DataAccess.Query.Entity.Wallet.User
{
    public class WalletDetailsQuery : QueryBaseEntity, IRequest<QueryResponseBO<WalletDetailsContract>>
    {
        public Guid Uid { get; set; }
        public string UserName { get; set; }

        public int WalletId { get; set; }
    }
}