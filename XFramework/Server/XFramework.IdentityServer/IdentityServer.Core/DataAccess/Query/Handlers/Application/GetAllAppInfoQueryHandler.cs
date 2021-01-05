using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using IdentityServer.Core.DataAccess.Query.Entity.Application;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.BusinessObject;
using IdentityServer.Domain.Contracts;
using IdentityServer.Domain.DataTableObject;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Application
{
    public class GetAllAppInfoQueryHandler : QueryBaseHandler, IRequestHandler<GetAppAppListQuery, QueryResponseBO<List<GetApplicationListContract>>>
    {
        public GetAllAppInfoQueryHandler(IDataLayer dataLayer, IMapper mapper)
        {
            _mapper = mapper;
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

            var r = _mapper.Map<List<TblApplication>,List<GetApplicationListContract>>(result);
            return new QueryResponseBO<List<GetApplicationListContract>>()
            {
                Response = r
            };
        }
    }
}