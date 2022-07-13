using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalEntityListHandler : QueryBaseHandler, IRequestHandler<GetHospitalEntityListQuery, QueryResponse<List<HospitalEntityResponse>>>
{
    public GetHospitalEntityListHandler()
    {
        
    }
    public async Task<QueryResponse<List<HospitalEntityResponse>>> Handle(GetHospitalEntityListQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.HealthEssentialsContext.HospitalEntities
            .Include(x => x.Group)
            .Where(x => EF.Functions.Like(x.Name, $"{request.SearchField}"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!entity.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Hospital Entity Found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Hospital Entity Found",
            IsSuccess = true,
            Response = entity.Adapt<List<HospitalEntityResponse>>()
        };
    }
}