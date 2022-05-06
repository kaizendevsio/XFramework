using IdentityServer.Core.DataAccess.Query.Entity.Address;
using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Address;

public class GetRegionListHandler : QueryBaseHandler, IRequestHandler<GetRegionListQuery, QueryResponse<List<AddressRegionResponse>>>
{
    public GetRegionListHandler(IDataLayer dataLayer, ICachingService cachingService)
    {
        _cachingService = cachingService;
        _dataLayer = dataLayer;
    }
    public async Task<QueryResponse<List<AddressRegionResponse>>> Handle(GetRegionListQuery request, CancellationToken cancellationToken)
    {
        /*if (_cachingService.AddressRegionResponseList.Any())
            return new ()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Response = _cachingService.AddressRegionResponseList.Adapt<List<AddressRegionResponse>>()
            };*/
        
        var entity = await _dataLayer.AddressRegions
            .Where(i => i.Country.Guid == $"{request.Guid}")
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!entity.Any())
        {
            return new ()
            {
                Message = "No data found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        //_cachingService.AddressRegionResponseList = entity.Adapt<List<AddressRegionResponse>>();

        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = entity.Adapt<List<AddressRegionResponse>>()
        };
    }
}