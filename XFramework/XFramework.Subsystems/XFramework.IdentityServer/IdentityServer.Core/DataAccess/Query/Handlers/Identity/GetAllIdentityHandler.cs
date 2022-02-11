using IdentityServer.Core.DataAccess.Query.Entity.Identity;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity;

public class GetAllIdentityHandler : QueryBaseHandler, IRequestHandler<GetAllIdentityQuery, QueryResponseBO<List<IdentityResponse>>>
{
    public GetAllIdentityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<QueryResponseBO<List<IdentityResponse>>> Handle(GetAllIdentityQuery request, CancellationToken cancellationToken)
    {
        var result = await _dataLayer.TblIdentityInformations
            .Take(1000)
            .AsNoTracking()
            .ToListAsync(cancellationToken: cancellationToken);
            
        if (!result.Any())
        {
            return new QueryResponseBO<List<IdentityResponse>>()
            {
                Message = $"No identity exist",
                HttpStatusCode = HttpStatusCode.NoContent
            };
        }

        var r = result.Adapt<List<IdentityResponse>>();
        return new QueryResponseBO<List<IdentityResponse>>()
        {
            Response = r
        };
    }
}