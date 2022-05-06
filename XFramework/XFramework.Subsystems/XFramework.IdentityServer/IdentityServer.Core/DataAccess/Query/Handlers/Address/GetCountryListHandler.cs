using IdentityServer.Core.DataAccess.Query.Entity.Address;
using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Address;

public class GetCountryListHandler : QueryBaseHandler, IRequestHandler<GetCountryListQuery, QueryResponse<List<AddressCountryResponse>>>
{
    public GetCountryListHandler(IDataLayer dataLayer, ICachingService cachingService)
    {
        _cachingService = cachingService;
        _dataLayer = dataLayer;
    }
    public async Task<QueryResponse<List<AddressCountryResponse>>> Handle(GetCountryListQuery request, CancellationToken cancellationToken)
    {
        /*if (_cachingService.AddressCountryResponseList.Any())
            return new ()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Response = _cachingService.AddressCountryResponseList.Adapt<List<AddressCountryResponse>>()
            };*/
        
        var entity = await _dataLayer.AddressCountries
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
        
        //_cachingService.AddressCountryResponseList = entity.Adapt<List<AddressCountryResponse>>();

        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = entity.Adapt<List<AddressCountryResponse>>()
        };
    }
}