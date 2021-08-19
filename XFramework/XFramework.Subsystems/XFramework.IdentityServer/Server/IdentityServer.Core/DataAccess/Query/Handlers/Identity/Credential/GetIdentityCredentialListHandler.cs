using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Query.Entity.Identity.Credential;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.Generic.Contracts.Responses;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Interfaces.Wrappers;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity.Credential
{
    public class GetIdentityCredentialListHandler : QueryBaseHandler, IRequestHandler<GetIdentityCredentialListQuery, QueryResponseBO<List<IdentityCredentialContract>>>
    {

        public GetIdentityCredentialListHandler(IDataLayer dataLayer, ICachingService cachingService, IHelperService helperService, JwtOptionsBO jwtOptionsBo, IJwtService jwtService, ILoggerWrapper recordsWrapper)
        {
            _dataLayer = dataLayer;
            _cachingService = cachingService;
        }
        
        public async Task<QueryResponseBO<List<IdentityCredentialContract>>> Handle(GetIdentityCredentialListQuery request, CancellationToken cancellationToken)
        {
            var result = await _dataLayer.TblIdentityRoles
                .Take(1000)
                .Include(i => i.UserCred)
                .ThenInclude(i => i.IdentityInfo)
                .AsNoTracking()
                .Where(i => i.RoleEntityId == (decimal?)request.IdentityRole)
                .Select(i => i.UserCred)
                .ToListAsync(cancellationToken: cancellationToken);
            
            if (!result.Any())
            {
                return new ()
                {
                    Message = $"No identity exist",
                    HttpStatusCode = HttpStatusCode.NoContent
                };
            }

            return new ()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Response = result.Adapt<List<IdentityCredentialContract>>()
            };
        }
    }
}