using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Doctor;

public class GetDoctorEntityGroupListHandler : QueryBaseHandler, IRequestHandler<GetDoctorEntityGroupListQuery, QueryResponse<List<DoctorEntityGroupResponse>>>
{
    public GetDoctorEntityGroupListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<DoctorEntityGroupResponse>>> Handle(GetDoctorEntityGroupListQuery request, CancellationToken cancellationToken)
    {
        var group = await _dataLayer.HealthEssentialsContext.DoctorEntityGroups
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
                Message = "No records found",
                IsSuccess = true
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Records found",
            Response = group.Adapt<List<DoctorEntityGroupResponse>>()
        };
    }
}