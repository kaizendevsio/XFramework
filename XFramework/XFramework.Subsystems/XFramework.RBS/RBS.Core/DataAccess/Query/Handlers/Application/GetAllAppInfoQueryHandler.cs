using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RBS.Core.DataAccess.Query.Entity.Application;
using RBS.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using RBS.Domain.BusinessObjects;
using RBS.Domain.Contracts;

namespace RBS.Core.DataAccess.Query.Handlers.Application
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