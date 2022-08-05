using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Doctor;

public class GetDoctorEntityListHandler : QueryBaseHandler, IRequestHandler<GetDoctorEntityListQuery, QueryResponse<List<DoctorEntityResponse>>>
{
    public GetDoctorEntityListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<DoctorEntityResponse>>> Handle(GetDoctorEntityListQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.HealthEssentialsContext.DoctorEntities
            .Include(x => x.Group)
            .Where(x => EF.Functions.ILike(x.Name, $"%{request.SearchField}%"))
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
                Message = "No records found",
                IsSuccess = true
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Records found",
            Response = entity.Adapt<List<DoctorEntityResponse>>()
        };
    }
}