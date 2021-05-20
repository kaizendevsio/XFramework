using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wallets.Core.DataAccess.Commands.Handlers;
using Wallets.Core.DataAccess.Query.Entity.Wallets;
using Wallets.Core.Interfaces;
using Wallets.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Core.DataAccess.Query.Handlers.Wallets
{
    public class GetAllWalletHandler : QueryBaseHandler, IRequestHandler<GetAllWalletQuery, QueryResponseBO<List<GetWalletContract>>>
    {
        public GetAllWalletHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        
        public async Task<QueryResponseBO<List<GetWalletContract>>> Handle(GetAllWalletQuery request, CancellationToken cancellationToken)
        {
            var result = await _dataLayer.TblWalletEntities.Take(1000).AsNoTracking().ToListAsync(cancellationToken: cancellationToken);
            if (!result.Any())
            {
                return new QueryResponseBO<List<GetWalletContract>>()
                {
                    Message = $"No identity exist",
                    HttpStatusCode = HttpStatusCode.NoContent
                };
            }

            var r = result.Adapt<List<GetWalletContract>>();
            return new QueryResponseBO<List<GetWalletContract>>()
            {
                Response = r
            };
        }
    }
}