using IdentityServer.Domain.BusinessObjects;
using IdentityServer.Domain.Contracts;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Records.Core.DataAccess.Query.Entity.Identity;
using Records.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Records.Core.DataAccess.Query.Handlers.Identity
{
    public class GetAllIdentityHandler : QueryBaseHandler, IRequestHandler<GetAllIdentityQuery, QueryResponseBO<List<GetIdentityContract>>>
    {
        public GetAllIdentityHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        public async Task<QueryResponseBO<List<GetIdentityContract>>> Handle(GetAllIdentityQuery request, CancellationToken cancellationToken)
        {
            var result = await _dataLayer.TblIdentityInformations.Take(1000).AsNoTracking().ToListAsync(cancellationToken: cancellationToken);
            if (!result.Any())
            {
                return new QueryResponseBO<List<GetIdentityContract>>()
                {
                    Message = $"No identity exist",
                    HttpStatusCode = HttpStatusCode.NoContent
                };
            }

            var r = result.Adapt<List<GetIdentityContract>>();
            return new QueryResponseBO<List<GetIdentityContract>>()
            {
                Response = r
            };
        }
    }
}