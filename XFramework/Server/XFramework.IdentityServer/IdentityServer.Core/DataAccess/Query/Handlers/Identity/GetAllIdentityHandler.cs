using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using IdentityServer.Core.DataAccess.Query.Entity.Identity;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.BusinessObject;
using IdentityServer.Domain.Contracts;
using IdentityServer.Domain.DataTableObject;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity
{
    public class GetAllIdentityHandler : QueryBaseHandler, IRequestHandler<GetAllIdentityQuery, QueryResponseBO<List<GetIdentityContract>>>
    {
        public GetAllIdentityHandler(IDataLayer dataLayer, IMapper mapper)
        {
            _dataLayer = dataLayer;
            _mapper = mapper;
        }
        public async Task<QueryResponseBO<List<GetIdentityContract>>> Handle(GetAllIdentityQuery request, CancellationToken cancellationToken)
        {
            var result = await _dataLayer.TblIdentityInfos.ToListAsync(cancellationToken: cancellationToken);
            if (!result.Any())
            {
                return new QueryResponseBO<List<GetIdentityContract>>()
                {
                    Message = $"No identity exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }

            var r = _mapper.Map<List<TblIdentityInfo>,List<GetIdentityContract>>(result);
            return new QueryResponseBO<List<GetIdentityContract>>()
            {
                Response = r
            };
        }
    }
}