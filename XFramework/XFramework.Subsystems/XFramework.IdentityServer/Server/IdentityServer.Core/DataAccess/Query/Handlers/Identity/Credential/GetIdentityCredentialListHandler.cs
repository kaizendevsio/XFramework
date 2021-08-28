using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Query.Entity.Identity.Credential;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.DataTransferObjects;
using IdentityServer.Domain.Generic.Contracts.Responses;
using IdentityServer.Domain.Generic.Enums;
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
            List<TblIdentityCredential> result = new();
            if (request.Filter)
            {
                result = await _dataLayer.TblIdentityRoles
                    .Include(i => i.UserCred)
                    .ThenInclude(i => i.TblIdentityContacts)
                    .ThenInclude(i => i.Ucentities)
                    .Include(i => i.UserCred)
                    .ThenInclude(i => i.IdentityInfo)
                    .AsNoTracking()
                    .Where(i => i.RoleEntityId == (decimal?)request.IdentityRole)
                    .Where(i => i.UserCred.ApplicationId == request.RequestServer.ApplicationId)
                    .Where(i => i.UserCred.IdentityInfo.FirstName.Contains(request.SearchString, StringComparison.InvariantCultureIgnoreCase))
                    .Where(i => i.UserCred.IdentityInfo.LastName.Contains(request.SearchString, StringComparison.InvariantCultureIgnoreCase))
                    .Where(i => i.UserCred.TblIdentityContacts.Any( o => o.Value.Contains(request.SearchString, StringComparison.InvariantCultureIgnoreCase)))
                    .Where(i => i.UserCred.UserName.Contains(request.SearchString, StringComparison.InvariantCultureIgnoreCase))
                    .Take(100)
                    .Select(i => i.UserCred)
                    .ToListAsync(cancellationToken: cancellationToken);
            }
            else
            {
                result = await _dataLayer.TblIdentityRoles
                    .Include(i => i.UserCred.TblIdentityContacts)
                    .ThenInclude(i => i.Ucentities)
                    .Include(i => i.UserCred.IdentityInfo)
                    .Include(i => i.UserCred.TblIdentityRoles)
                    .AsNoTracking()
                    .Where(i => i.RoleEntityId != (decimal?)RoleEntity.Client)
                    .Where(i => i.UserCred.ApplicationId == request.RequestServer.ApplicationId)
                    .Where(i => i.UserCred.IdentityInfo.FirstName.Contains(request.SearchString, StringComparison.InvariantCultureIgnoreCase))
                    .Where(i => i.UserCred.IdentityInfo.LastName.Contains(request.SearchString, StringComparison.InvariantCultureIgnoreCase))
                    .Where(i => i.UserCred.TblIdentityContacts.Any( o => o.Value.Contains(request.SearchString, StringComparison.InvariantCultureIgnoreCase)))
                    .Where(i => i.UserCred.UserName.Contains(request.SearchString, StringComparison.InvariantCultureIgnoreCase))
                    .Take(100)
                    .Select(i => i.UserCred)
                    .ToListAsync(cancellationToken: cancellationToken);
            }
            
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