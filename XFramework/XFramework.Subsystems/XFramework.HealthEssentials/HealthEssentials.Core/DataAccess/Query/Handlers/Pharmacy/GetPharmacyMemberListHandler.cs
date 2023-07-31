using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyMemberListHandler : QueryBaseHandler, IRequestHandler<GetPharmacyMemberListQuery, QueryResponse<List<PharmacyMemberResponse>>>
{
    public GetPharmacyMemberListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PharmacyMemberResponse>>> Handle(GetPharmacyMemberListQuery request, CancellationToken cancellationToken)
    {
        var pharmacyMember = await _dataLayer.HealthEssentialsContext.PharmacyMembers
            .Include(x => x.PharmacyLocation)
            .ThenInclude(i => i.Pharmacy)
            .Where(i => EF.Functions.ILike(i.Name, $"%{request.SearchField}%"))
            .OrderBy(i => i.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        if (!pharmacyMember.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Pharmacy Member Found",
                
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Pharmacy Member Found",
            
            Response = pharmacyMember.Adapt<List<PharmacyMemberResponse>>()
        };
    }
}