using IdentityServer.Core.DataAccess.Query.Entity.Identity;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity;

public class GetAllIdentityHandler : QueryBaseHandler, IRequestHandler<GetAllIdentityQuery, QueryResponse<List<IdentityResponse>>>
{
    public GetAllIdentityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<QueryResponse<List<IdentityResponse>>> Handle(GetAllIdentityQuery request, CancellationToken cancellationToken)
    {
        var result = await _dataLayer.TblIdentityInformations
            .Take(1000)
            .AsNoTracking()
            .ToListAsync(cancellationToken: cancellationToken);
            
        if (!result.Any())
        {
            return new QueryResponse<List<IdentityResponse>>()
            {
                Message = $"No identity exist",
                HttpStatusCode = HttpStatusCode.NoContent
            };
        }

        var r = result.Adapt<List<IdentityResponse>>();
        return new QueryResponse<List<IdentityResponse>>()
        {
            Response = r
        };
    }
}