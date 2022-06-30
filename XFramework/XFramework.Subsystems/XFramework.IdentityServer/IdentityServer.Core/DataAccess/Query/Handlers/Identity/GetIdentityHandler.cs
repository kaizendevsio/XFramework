using IdentityServer.Core.DataAccess.Query.Entity.Identity;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity;

public class GetIdentityHandler : QueryBaseHandler ,IRequestHandler<GetIdentityQuery, QueryResponse<IdentityResponse>>
{
    public GetIdentityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<IdentityResponse>> Handle(GetIdentityQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.IdentityInformations.FirstOrDefaultAsync(i => i.Guid == request.Guid.ToString(), cancellationToken: cancellationToken);
           
        if (entity == null)
        {
            return new QueryResponse<IdentityResponse>()
            {
                Message = $"Identity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        return new QueryResponse<IdentityResponse>()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = entity.Adapt<IdentityResponse>()
        };
            
    }
}