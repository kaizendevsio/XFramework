using IdentityServer.Core.DataAccess.Query.Entity.Address;
using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Address;

public class GetCityListHandler : QueryBaseHandler, IRequestHandler<GetCityListQuery, QueryResponse<List<AddressCityResponse>>>
{
    public GetCityListHandler(IDataLayer dataLayer, ICachingService cachingService)
    {
        _cachingService = cachingService;
        _dataLayer = dataLayer;
    }
    public async Task<QueryResponse<List<AddressCityResponse>>> Handle(GetCityListQuery request, CancellationToken cancellationToken)
    {
        /*if (_cachingService.AddressCityResponseList.Any())
            return new ()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Response = _cachingService.AddressCityResponseList.Adapt<List<AddressCityResponse>>()
            };*/
        
        var entity = await _dataLayer.AddressCities
            .Where(i => i.ProvCodeNavigation.Guid == $"{request.Guid}")
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
        
        //_cachingService.AddressCityResponseList = entity.Adapt<List<AddressCityResponse>>();

        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = entity.Adapt<List<AddressCityResponse>>()
        };
    }
}