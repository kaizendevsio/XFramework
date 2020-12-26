using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Query.Entity.Application;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.BO;
using IdentityServer.Domain.Contracts;
using IdentityServer.Domain.DTO;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Application
{
    public class AppsInfoQueryHandler : QueryBaseHandler, IRequestHandler<AppsListQuery, QueryResponseBO<List<GetApplicationListContract>>>
    {
        public AppsInfoQueryHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        public async Task<QueryResponseBO<List<GetApplicationListContract>>> Handle(AppsListQuery request, CancellationToken cancellationToken)
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
            
            return new QueryResponseBO<List<GetApplicationListContract>>()
            {
                Response = _mapper.Map<List<GetApplicationListContract>>(result)
            };
        }
    }
}