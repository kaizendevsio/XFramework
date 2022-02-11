using IdentityServer.Core.DataAccess.Query.Entity.Identity;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity;

public class GetIdentityHandler : QueryBaseHandler ,IRequestHandler<GetIdentityQuery, QueryResponseBO<IdentityResponse>>
{
    public GetIdentityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponseBO<IdentityResponse>> Handle(GetIdentityQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.TblIdentityInformations.FirstOrDefaultAsync(i => i.Guid == request.Guid.ToString(), cancellationToken: cancellationToken);
           
        if (entity == null)
        {
            return new QueryResponseBO<IdentityResponse>()
            {
                Message = $"Identity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        return new QueryResponseBO<IdentityResponse>()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = entity.Adapt<IdentityResponse>()
        };
            
    }
}