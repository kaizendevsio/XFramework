using IdentityServer.Core.DataAccess.Query.Entity.Address;
using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Address;

public class GetBarangayListHandler : QueryBaseHandler, IRequestHandler<GetBarangayListQuery, QueryResponse<List<AddressBarangayResponse>>>
{
    public GetBarangayListHandler(IDataLayer dataLayer, ICachingService cachingService)
    {
        _cachingService = cachingService;
        _dataLayer = dataLayer;
    }
    public async Task<QueryResponse<List<AddressBarangayResponse>>> Handle(GetBarangayListQuery request, CancellationToken cancellationToken)
    {
        /*if (_cachingService.AddressBarangayResponseList.Any())
            return new ()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Response = _cachingService.AddressBarangayResponseList.Adapt<List<AddressBarangayResponse>>()
            };*/
        
        var entity = await _dataLayer.AddressBarangays
            .Where(i => i.CityCodeNavigation.Guid == $"{request.Guid}")
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
        
        //_cachingService.AddressBarangayResponseList = entity.Adapt<List<AddressBarangayResponse>>();

        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = entity.Adapt<List<AddressBarangayResponse>>()
        };
    }
}