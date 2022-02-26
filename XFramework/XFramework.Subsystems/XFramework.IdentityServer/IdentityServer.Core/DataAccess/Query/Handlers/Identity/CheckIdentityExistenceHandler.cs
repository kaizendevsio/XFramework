using IdentityServer.Core.DataAccess.Query.Entity.Identity;
using IdentityServer.Domain.DataTransferObjects.Legacy;
using XFramework.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity;

public class CheckIdentityExistenceHandler : QueryBaseHandler ,IRequestHandler<CheckIdentityExistenceQuery, QueryResponseBO<ExistenceResponse>>
{
    private readonly LegacyContext _legacyContext;

    public CheckIdentityExistenceHandler(IDataLayer dataLayer, LegacyContext legacyContext)
    {
        _legacyContext = legacyContext;
        _dataLayer = dataLayer;
    }
        
    public async Task<QueryResponseBO<ExistenceResponse>> Handle(CheckIdentityExistenceQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.FirstName) && string.IsNullOrEmpty(request.MiddleName) && string.IsNullOrEmpty(request.LastName))
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted
            };
        }
        
        var existing = _dataLayer.TblIdentityInformations
            .AsNoTracking()
            .Where(i => i.FirstName == request.FirstName)
            .Where(i  => i.MiddleName == request.MiddleName)
            .Where(i => i.LastName == request.LastName)
            .Where(i => i.Guid != $"{request.Guid}")
            .Any();
            
        if (existing)
        {
            return new ()
            {
                Message = $"The identity with name '{request.FirstName} {request.MiddleName} {request.LastName}' already exists",
                HttpStatusCode = HttpStatusCode.Conflict
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}