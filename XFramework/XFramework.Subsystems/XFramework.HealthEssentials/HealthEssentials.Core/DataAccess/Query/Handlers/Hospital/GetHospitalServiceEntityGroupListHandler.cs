using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalServiceEntityGroupListHandler : QueryBaseHandler, IRequestHandler<GetHospitalServiceEntityGroupListQuery, QueryResponse<List<HospitalServiceEntityGroupResponse>>>
{
    public GetHospitalServiceEntityGroupListHandler()
    {
        
    }
    public async Task<QueryResponse<List<HospitalServiceEntityGroupResponse>>> Handle(GetHospitalServiceEntityGroupListQuery request, CancellationToken cancellationToken)
    {
        var serviceEntityGroup = await _dataLayer.HealthEssentialsContext.HospitalServiceEntityGroups
            .Where(x => EF.Functions.Like(x.Name, $"{request.SearchField}"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
            
        if (!serviceEntityGroup.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Hospital Service Entity Group Found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Hospital Service Entity Group Found",
            IsSuccess = true,
            Response = serviceEntityGroup.Adapt<List<HospitalServiceEntityGroupResponse>>()
        };

    }
}