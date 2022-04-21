using IdentityServer.Core.DataAccess.Query.Entity.Identity.Address;
using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity.Address;

public class GetLocationListHandler : QueryBaseHandler, IRequestHandler<GetLocationListQuery, QueryResponse<List<IdentityLocationResponse>>>
{
    public GetLocationListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<IdentityLocationResponse>>> Handle(GetLocationListQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.IdentityAddresses
            .Include(i => i.AddressEntities)
            .Include(i => i.CountryNavigation)
            .ThenInclude(i => i.AddressRegions)
            .ThenInclude(i => i.AddressProvinces)
            .ThenInclude(i => i.AddressCities)
            .ThenInclude(i => i.AddressBarangays)
            .Where(i => i.IdentityInfo.Guid == $"{request.IdentityGuid}")
            .AsSplitQuery()
            .AsNoTracking()
            .Select(i => i.AddressEntities)
            .ToListAsync(CancellationToken.None);
       
        if (!entity.Any())
        {
            return new ()
            {
                Message = $"Identity with Guid {request.IdentityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = entity.Adapt<List<IdentityLocationResponse>>()
        };
        
    }
}