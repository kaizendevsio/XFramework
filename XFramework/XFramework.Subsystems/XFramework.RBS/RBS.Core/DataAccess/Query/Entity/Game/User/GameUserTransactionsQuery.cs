using System.Collections.Generic;
using MediatR;
using RBS.Core.DataAccess.Query.Entity.Wallet.User;
using RBS.Domain.BusinessObjects;
using RBS.Domain.Contracts.Responses;

namespace RBS.Core.DataAccess.Query.Entity.Game.User
{
    public class GameUserTransactionsQuery : QueryBaseEntity, IRequest<QueryResponseBO<List<GameTransactionsContract>>>
    {
        public string UserName { get; set; }
    }
}