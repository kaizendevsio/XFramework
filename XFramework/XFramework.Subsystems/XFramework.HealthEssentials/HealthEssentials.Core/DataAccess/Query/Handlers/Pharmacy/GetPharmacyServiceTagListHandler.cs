using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyServiceTagListHandler : QueryBaseHandler, IRequestHandler<GetPharmacyServiceTagListQuery, QueryResponse<List<PharmacyServiceTagResponse>>>
{
    public GetPharmacyServiceTagListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PharmacyServiceTagResponse>>> Handle(GetPharmacyServiceTagListQuery request, CancellationToken cancellationToken)
    {
        var serviceTag = await _dataLayer.HealthEssentialsContext.PharmacyServiceTags
            .Include(x => x.PharmacyService)
            .Include(x => x.Tag)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .ToListAsync(CancellationToken.None);
        
        if (!serviceTag.Any())
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
            
            Response = serviceTag.Adapt<List<PharmacyServiceTagResponse>>()
        };
    }
}