using IdentityServer.Core.DataAccess.Query.Entity.Identity.Address;
using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity.Address;

public class GetLocationHandler : QueryBaseHandler ,IRequestHandler<GetLocationQuery, QueryResponse<IdentityLocationResponse>>
{
    
    public GetLocationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<IdentityLocationResponse>> Handle(GetLocationQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.TblIdentityAddresses
            .Include(i => i.AddressEntities)
            .Include(i => i.CountryNavigation)
            .ThenInclude(i => i.TblAddressRegions)
            .ThenInclude(i => i.TblAddressProvinces)
            .ThenInclude(i => i.TblAddressCities)
            .ThenInclude(i => i.TblAddressBarangays)
            .AsSplitQuery()
            .AsNoTracking()
            .Where(i => i.AddressEntities.Guid == $"{request.LocationGuid}")
            .Select(i => i.AddressEntities)
            .FirstOrDefaultAsync(CancellationToken.None);
           
        if (entity == null)
        {
            return new ()
            {
                Message = $"Location with Guid {request.LocationGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = entity.Adapt<IdentityLocationResponse>()
        };
    }
}