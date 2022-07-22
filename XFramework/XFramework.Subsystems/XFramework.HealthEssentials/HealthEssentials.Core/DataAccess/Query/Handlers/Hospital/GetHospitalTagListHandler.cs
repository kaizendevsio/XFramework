using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalTagListHandler : QueryBaseHandler, IRequestHandler<GetHospitalTagListQuery, QueryResponse<List<HospitalTagResponse>>>
{
    public GetHospitalTagListHandler()
    {
        
    }
    public async Task<QueryResponse<List<HospitalTagResponse>>> Handle(GetHospitalTagListQuery request, CancellationToken cancellationToken)
    {
        var tag = await _dataLayer.HealthEssentialsContext.HospitalTags
            .Include(x => x.Tag)
            .Include(x => x.Hospital)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!tag.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Hospital Tags found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Hospital Tag Found",
            IsSuccess = true,
            Response = tag.Adapt<List<HospitalTagResponse>>()
        };
    }
}