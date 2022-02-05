using IdentityServer.Core.DataAccess.Query.Entity.Identity.Credentials;
using IdentityServer.Domain.DataTransferObjects.Legacy;
using XFramework.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity.Credential;

public class CheckCredentialExistenceHandler : QueryBaseHandler ,IRequestHandler<CheckCredentialExistenceQuery, QueryResponseBO<ExistenceResponse>>
{
    private readonly LegacyContext _legacyContext;

    public CheckCredentialExistenceHandler(IDataLayer dataLayer, LegacyContext legacyContext)
    {
        _legacyContext = legacyContext;
        _dataLayer = dataLayer;
    }
    public async Task<QueryResponseBO<ExistenceResponse>> Handle(CheckCredentialExistenceQuery request, CancellationToken cancellationToken)
    {
        var existing = _dataLayer.TblIdentityCredentials
            .AsNoTracking()
            .Where(i => i.UserName == request.UserName)
            .Where(i => i.Guid != request.Cuid)
            .Any();
            
        if (existing)
        {
            return new ()
            {
                Message = $"The identity with username '{request.UserName}' already exists",
                HttpStatusCode = HttpStatusCode.Conflict
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}