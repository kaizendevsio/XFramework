using IdentityServer.Core.DataAccess.Query.Entity.Identity.Credentials;
using IdentityServer.Domain.DataTransferObjects.Legacy;
using IdentityServer.Domain.Generic.Contracts.Responses;
using IdentityServer.Domain.Generic.Enums;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Interfaces.Wrappers;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity.Credential;

public class GetCredentialListHandler : QueryBaseHandler, IRequestHandler<GetCredentialListQuery, QueryResponseBO<List<CredentialResponse>>>
{

    public GetCredentialListHandler(IDataLayer dataLayer, ICachingService cachingService, IHelperService helperService, JwtOptionsBO jwtOptionsBo, IJwtService jwtService, ILoggerWrapper recordsWrapper)
    {
        _dataLayer = dataLayer;
        _cachingService = cachingService;
    }
        
    public async Task<QueryResponseBO<List<CredentialResponse>>> Handle(GetCredentialListQuery request, CancellationToken cancellationToken)
    {
        
        var appEntity = await _dataLayer.TblApplications.FirstOrDefaultAsync(i => i.Guid == $"{request.ApplicationGuid}", cancellationToken);
        if (appEntity == null)
        {
            return new ()
            {
                Message = $"Identity with Guid {request.ApplicationGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var result = await _dataLayer.TblIdentityCredentials
            .Where(i => i.ApplicationId == appEntity.Id)
            .AsNoTracking()
            .ToListAsync(cancellationToken: cancellationToken);

        if (!result.Any())
        {
            return new ()
            {
                Message = $"No credentials exist",
                HttpStatusCode = HttpStatusCode.NoContent
            };
        }

        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = result.Adapt<List<CredentialResponse>>()
        };
    }

}