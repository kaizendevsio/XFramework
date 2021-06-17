using System;
using System.Collections.Generic;
using MediatR;
using RBS.Domain.BusinessObjects;
using RBS.Domain.Contracts.Responses;

namespace RBS.Core.DataAccess.Query.Entity.Wallet.User
{
    public class WalletTransactionsQuery : QueryBaseEntity, IRequest<QueryResponseBO<List<WalletTransactionContract>>>
    {
        public Guid Uid { get; set; }
        public string UserName { get; set; }

        public int WalletId { get; set; }
    }
}