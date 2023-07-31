using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalServiceEntityListHandler : QueryBaseHandler, IRequestHandler<GetHospitalServiceEntityListQuery, QueryResponse<List<HospitalServiceEntityResponse>>>
{
    public GetHospitalServiceEntityListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<HospitalServiceEntityResponse>>> Handle(GetHospitalServiceEntityListQuery request, CancellationToken cancellationToken)
    {
        var serviceEntity = await _dataLayer.HealthEssentialsContext.HospitalServiceEntities
            .Include(x => x.Group)
            .Where(x => EF.Functions.ILike(x.Name, $"%{request.SearchField}%"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!serviceEntity.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Hospital Service Entities found",
                
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Hospital Service Entity Found",
            
            Response = serviceEntity.Adapt<List<HospitalServiceEntityResponse>>()
        };
    }
}