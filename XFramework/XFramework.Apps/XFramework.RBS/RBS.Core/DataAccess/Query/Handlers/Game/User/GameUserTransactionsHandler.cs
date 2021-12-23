using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RBS.Core.DataAccess.Query.Entity.Game.User;
using RBS.Core.Interfaces;
using RBS.Domain.BusinessObjects;
using RBS.Domain.Contracts.Responses;

namespace RBS.Core.DataAccess.Query.Handlers.Game.User
{
    public class GameUserTransactionsHandler : QueryBaseHandler, IRequestHandler<GameUserTransactionsQuery, QueryResponseBO<List<GameTransactionsContract>>>
    {
        public GameUserTransactionsHandler(IMediator mediator, IDataLayer dataLayer)
        {
            _mediator = mediator;
            _dataLayer = dataLayer;
        }
        
        public async Task<QueryResponseBO<List<GameTransactionsContract>>> Handle(GameUserTransactionsQuery request, CancellationToken cancellationToken)
        {
            var user = await _dataLayer.TblIdentityCredentials
                .Where(i => i.UserName == request.UserName)
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);
                
            if (user == null)
            {
                return new()
                {
                    Message = "The requested resource was not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            
            var result = await _dataLayer.FightTransactions
                .Where(i => i.UserAuth.UserName == request.UserName)
                .ToListAsync(cancellationToken: cancellationToken);

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Response = result.Adapt<List<GameTransactionsContract>>()
            };
        }
    }
}