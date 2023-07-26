using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyTagListHandler : QueryBaseHandler, IRequestHandler<GetPharmacyTagListQuery, QueryResponse<List<PharmacyTagResponse>>>
{
    public GetPharmacyTagListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PharmacyTagResponse>>> Handle(GetPharmacyTagListQuery request, CancellationToken cancellationToken)
    {
        var pharmacyTag = await _dataLayer.HealthEssentialsContext.PharmacyTags
            .Include(x => x.Pharmacy)
            .Include(x => x.Tag)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!pharmacyTag.Any())
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
            Message = "Data found",
            
            Response = pharmacyTag.Adapt<List<PharmacyTagResponse>>()
        };
    }
}