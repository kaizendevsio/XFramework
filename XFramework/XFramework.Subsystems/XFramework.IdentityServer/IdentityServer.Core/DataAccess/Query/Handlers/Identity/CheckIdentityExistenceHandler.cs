using IdentityServer.Core.DataAccess.Query.Entity.Identity;
using XFramework.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity;

public class CheckIdentityExistenceHandler : QueryBaseHandler ,IRequestHandler<CheckIdentityExistenceQuery, QueryResponse<ExistenceResponse>>
{
    public CheckIdentityExistenceHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
        
    public async Task<QueryResponse<ExistenceResponse>> Handle(CheckIdentityExistenceQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.FirstName) && string.IsNullOrEmpty(request.MiddleName) && string.IsNullOrEmpty(request.LastName))
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted
            };
        }
        
        var existing = _dataLayer.IdentityInformations
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