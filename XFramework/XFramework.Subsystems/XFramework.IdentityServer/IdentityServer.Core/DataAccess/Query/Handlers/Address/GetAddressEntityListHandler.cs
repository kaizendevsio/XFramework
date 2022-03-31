using IdentityServer.Core.DataAccess.Query.Entity.Address;
using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Address;

public class GetAddressEntityListHandler : QueryBaseHandler, IRequestHandler<GetAddressEntityListQuery, QueryResponse<List<AddressCountryResponse>>>
{
    public GetAddressEntityListHandler(IDataLayer dataLayer, ICachingService cachingService)
    {
        _cachingService = cachingService;
        _dataLayer = dataLayer;
    }
    public async Task<QueryResponse<List<AddressCountryResponse>>> Handle(GetAddressEntityListQuery request, CancellationToken cancellationToken)
    {
        if (_cachingService.AddressCountryResponseList.Any())
            return new ()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Response = _cachingService.AddressCountryResponseList.Adapt<List<AddressCountryResponse>>()
            };
        
        var entity = await _dataLayer.TblAddressCountries
            .Include(i => i.TblAddressRegions)
            .ThenInclude(i => i.TblAddressProvinces)
            .ThenInclude(i => i.TblAddressCities)
            .ThenInclude(i => i.TblAddressBarangays)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!entity.Any())
        {
            return new ()
            {
                Message = $"No address entity exists",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        _cachingService.AddressCountryResponseList = entity.Adapt<List<AddressCountryResponse>>();

        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = _cachingService.AddressCountryResponseList
        };
    }
}