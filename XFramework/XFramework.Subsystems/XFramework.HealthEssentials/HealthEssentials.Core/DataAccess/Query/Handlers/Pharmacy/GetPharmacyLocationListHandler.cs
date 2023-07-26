using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyLocationListHandler : QueryBaseHandler, IRequestHandler<GetPharmacyLocationListQuery, QueryResponse<List<PharmacyLocationResponse>>>
{
    public GetPharmacyLocationListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PharmacyLocationResponse>>> Handle(GetPharmacyLocationListQuery request, CancellationToken cancellationToken)
    {
        var pharmacyLocation = await _dataLayer.HealthEssentialsContext.PharmacyLocations
            .Include(i => i.PharmacyServices)
            .Include(i => i.PharmacyMembers)
            .Include(i => i.Pharmacy)
            .Include(x => x.PharmacyJobOrders)
            .Where(i => EF.Functions.ILike(i.Name, $"%{request.SearchField}%"))
            .Where(x => x.Status == (int)request.Status)
            .OrderBy(i => i.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        if (!pharmacyLocation.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No data found",
                
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Pharmacy location found",
            
            Response = pharmacyLocation.Adapt<List<PharmacyLocationResponse>>()
        };
    }
}