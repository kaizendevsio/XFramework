using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyMemberHandler : QueryBaseHandler, IRequestHandler<GetPharmacyMemberQuery, QueryResponse<PharmacyMemberResponse>>
{
    public GetPharmacyMemberHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<PharmacyMemberResponse>> Handle(GetPharmacyMemberQuery request, CancellationToken cancellationToken)
    {
        var member = await _dataLayer.HealthEssentialsContext.PharmacyMembers
            .Include(x => x.PharmacyLocation)
            .ThenInclude(x => x.Pharmacy)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (member is null)
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
            Message = "Data Found",
            Response = member.Adapt<PharmacyMemberResponse>()
        };
    }
}