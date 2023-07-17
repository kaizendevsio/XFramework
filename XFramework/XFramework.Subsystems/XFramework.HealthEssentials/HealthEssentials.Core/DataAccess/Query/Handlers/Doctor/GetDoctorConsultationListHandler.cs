using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Doctor;

public class GetDoctorConsultationListHandler : QueryBaseHandler, IRequestHandler<GetDoctorConsultationListQuery, QueryResponse<List<DoctorConsultationResponse>>>
{
    public GetDoctorConsultationListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<DoctorConsultationResponse>>> Handle(GetDoctorConsultationListQuery request, CancellationToken cancellationToken)
    {
        var doctorConsultation = await _dataLayer.HealthEssentialsContext.DoctorConsultations
            .Include(x => x.Consultation)
            .ThenInclude(x => x.Entity)
            .Include(x => x.Doctor)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!doctorConsultation.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No records found",
                
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Records found",
            Response = doctorConsultation.Adapt<List<DoctorConsultationResponse>>()
        };
    }
}