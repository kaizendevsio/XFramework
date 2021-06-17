using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RBS.Core.DataAccess.Query.Entity.Identity;
using RBS.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using RBS.Domain.BusinessObjects;
using RBS.Domain.Contracts;

namespace RBS.Core.DataAccess.Query.Handlers.Identity
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