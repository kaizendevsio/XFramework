using IdentityServer.Core.DataAccess.Query.Entity.Address;
using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Address;

public class GetProvinceListHandler : QueryBaseHandler, IRequestHandler<GetProvinceListQuery, QueryResponse<List<AddressProvinceResponse>>>
{
    public GetProvinceListHandler(IDataLayer dataLayer, ICachingService cachingService)
    {
        _cachingService = cachingService;
        _dataLayer = dataLayer;
    }
    public async Task<QueryResponse<List<AddressProvinceResponse>>> Handle(GetProvinceListQuery request, CancellationToken cancellationToken)
    {
        /*if (_cachingService.AddressProvinceResponseList.Any())
            return new ()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Response = _cachingService.AddressProvinceResponseList.Adapt<List<AddressProvinceResponse>>()
            };*/
        
        var entity = await _dataLayer.AddressProvinces
            .Where(i => i.RegCodeNavigation.Guid == $"{request.Guid}")
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
        
        //_cachingService.AddressProvinceResponseList = entity.Adapt<List<AddressProvinceResponse>>();

        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = entity.Adapt<List<AddressProvinceResponse>>()
        };
    }
}