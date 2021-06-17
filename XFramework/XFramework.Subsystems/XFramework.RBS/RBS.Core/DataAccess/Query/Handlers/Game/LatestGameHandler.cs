using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RBS.Core.DataAccess.Query.Entity.Game;
using RBS.Core.Interfaces;
using RBS.Domain.BusinessObjects;
using RBS.Domain.Contracts.Responses;

namespace RBS.Core.DataAccess.Query.Handlers.Game
{
    public class LatestGameHandler : QueryBaseHandler, IRequestHandler<LatestGameQuery, QueryResponseBO<LatestGameContract>>
    {
        public LatestGameHandler(IMediator mediator, IDataLayer dataLayer)
        {
            _mediator = mediator;
            _dataLayer = dataLayer;
        }
        
        public async Task<QueryResponseBO<LatestGameContract>> Handle(LatestGameQuery request, CancellationToken cancellationToken)
        {
            var result = await _dataLayer.FightHistories.OrderBy(i => i.Id).LastOrDefaultAsync(cancellationToken);
          
            if (result == null)
            {
                return new()
                {
                    Message = "The requested resource was not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Response = result.Adapt<LatestGameContract>()
            };
        }
    }
}