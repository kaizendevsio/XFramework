using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Query.Entity.Roles;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.Generic.Contracts.Responses;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Generic.BusinessObjects;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Roles
{
    public class GetRoleEntityListHandler : QueryBaseHandler, IRequestHandler<GetRoleEntityListQuery, QueryResponseBO<List<IdentityRoleEntityContract>>>
    {
        public GetRoleEntityListHandler(IDataLayer dataLayer)
        {
            _dataLayer = dataLayer;
        }
        
        public async Task<QueryResponseBO<List<IdentityRoleEntityContract>>> Handle(GetRoleEntityListQuery request, CancellationToken cancellationToken)
        {
            var result = await _dataLayer.TblIdentityRoleEntities
                .Take(1000)
                .AsNoTracking()
                .ToListAsync(cancellationToken: cancellationToken);
            
            if (!result.Any())
            {
                return new ()
                {
                    Message = $"No identity role exist",
                    HttpStatusCode = HttpStatusCode.NoContent
                };
            }

            var r = result.Adapt<List<IdentityRoleEntityContract>>();
            return new ()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Response = r
            };    
        }
    }
}