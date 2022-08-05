using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalServiceTagListHandler : QueryBaseHandler, IRequestHandler<GetHospitalServiceTagListQuery, QueryResponse<List<HospitalServiceTagResponse>>>
{
    public GetHospitalServiceTagListHandler()
    {
        
    }
    public async Task<QueryResponse<List<HospitalServiceTagResponse>>> Handle(GetHospitalServiceTagListQuery request, CancellationToken cancellationToken)
    {
        var serviceTag = await _dataLayer.HealthEssentialsContext.HospitalServiceTags
            .Include(x => x.HospitalService)
            .Include(x => x.Tag)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!serviceTag.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Hospital Service Tag Found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Hospital Service Tag Found",
            IsSuccess = true,
            Response = serviceTag.Adapt<List<HospitalServiceTagResponse>>()
        };
    }
}