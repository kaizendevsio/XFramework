using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Doctor;

public class GetDoctorTagListHandler : QueryBaseHandler, IRequestHandler<GetDoctorTagListQuery, QueryResponse<List<DoctorTagResponse>>>
{
    public GetDoctorTagListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<DoctorTagResponse>>> Handle(GetDoctorTagListQuery request, CancellationToken cancellationToken)
    {
        var doctorTag = await _dataLayer.HealthEssentialsContext.DoctorTags
            .Include(x => x.Doctor)
            .Include(x => x.Tag)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!doctorTag.Any())
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
            Response = doctorTag.Adapt<List<DoctorTagResponse>>()
        };
    }
}