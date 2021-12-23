using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Query.Entity.Identity;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.Generic.Contracts.Responses;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Generic.BusinessObjects;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity
{
    public class GetAllIdentityHandler : QueryBaseHandler, IRequestHandler<GetAllIdentityQuery, QueryResponseBO<List<IdentityInfoContract>>>
    {
        public GetAllIdentityHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        public async Task<QueryResponseBO<List<IdentityInfoContract>>> Handle(GetAllIdentityQuery request, CancellationToken cancellationToken)
        {
            var result = await _dataLayer.TblIdentityInformations
                .Take(1000)
                .AsNoTracking()
                .ToListAsync(cancellationToken: cancellationToken);
            
            if (!result.Any())
            {
                return new QueryResponseBO<List<IdentityInfoContract>>()
                {
                    Message = $"No identity exist",
                    HttpStatusCode = HttpStatusCode.NoContent
                };
            }

            var r = result.Adapt<List<IdentityInfoContract>>();
            return new QueryResponseBO<List<IdentityInfoContract>>()
            {
                Response = r
            };
        }
    }
}