using System;
using System.Collections.Generic;
using MediatR;
using RBS.Core.DataAccess.Commands.Entity;
using RBS.Domain.BusinessObjects;
using RBS.Domain.Contracts.Responses;

namespace RBS.Core.DataAccess.Query.Entity.Wallet.User
{
    public class WalletListQuery : QueryBaseEntity, IRequest<QueryResponseBO<List<WalletDetailsContract>>>
    {
        public Guid Uid { get; set; }
        public string UserName { get; set; }

    }
}