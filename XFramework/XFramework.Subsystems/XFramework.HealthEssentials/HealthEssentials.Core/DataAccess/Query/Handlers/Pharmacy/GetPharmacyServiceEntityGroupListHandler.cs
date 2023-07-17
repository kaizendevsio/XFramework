using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyServiceEntityGroupListHandler : QueryBaseHandler, IRequestHandler<GetPharmacyServiceEntityGroupListQuery, QueryResponse<List<PharmacyServiceEntityGroupResponse>>>
{
    public GetPharmacyServiceEntityGroupListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PharmacyServiceEntityGroupResponse>>> Handle(GetPharmacyServiceEntityGroupListQuery request, CancellationToken cancellationToken)
    {
        var group = await _dataLayer.HealthEssentialsContext.PharmacyServiceEntityGroups
            .Where(x => EF.Functions.ILike(x.Name, $"%{request.SearchField}%"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        if (!group.Any())
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
            
            Response = group.Adapt<List<PharmacyServiceEntityGroupResponse>>()
        };
    }
}