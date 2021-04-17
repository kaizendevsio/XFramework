using Coins.Core.DataAccess.Query.Entity.Application;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Coins.Core.Interfaces;
using Coins.Domain.BusinessObjects;
using Coins.Domain.Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Generic.BusinessObjects;

namespace Coins.Core.DataAccess.Query.Handlers.Application
{
    public class GetAllAppInfoQueryHandler : QueryBaseHandler, IRequestHandler<GetAppAppListQuery, QueryResponseBO<List<GetApplicationListContract>>>
    {
        public GetAllAppInfoQueryHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        public async Task<QueryResponseBO<List<GetApplicationListContract>>> Handle(GetAppAppListQuery request, CancellationToken cancellationToken)
        {
            var result = await _dataLayer.TblApplications.ToListAsync(cancellationToken: cancellationToken);
            if (!result.Any())
            {
                return new QueryResponseBO<List<GetApplicationListContract>>()
                {
                    Message = $"No applications exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }

            var r = result.Adapt<List<GetApplicationListContract>>();
            return new QueryResponseBO<List<GetApplicationListContract>>()
            {
                Response = r
            };
        }
    }
}