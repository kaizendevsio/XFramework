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
            .Include(i => i.Pharmacy)
            .AsSplitQuery()
            .Where(i => EF.Functions.ILike(i.Name, $"%{request.SearchField}%"))
            .OrderBy(i => i.Name)
            .Take(request.PageSize)
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        if (!pharmacyMember.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Pharmacy Member Found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Pharmacy Member Found",
            IsSuccess = true,
            Response = pharmacyMember.Adapt<List<PharmacyMemberResponse>>()
        };
    }
}