using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Doctor;

public class GetDoctorConsultationHandler : QueryBaseHandler, IRequestHandler<GetDoctorConsultationQuery, QueryResponse<DoctorConsultationResponse>>
{
    public GetDoctorConsultationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<DoctorConsultationResponse>> Handle(GetDoctorConsultationQuery request, CancellationToken cancellationToken)
    {
        var consultation = await _dataLayer.HealthEssentialsContext.DoctorConsultations
            .Include(x => x.Consultation)
            .Include(x => x.Doctor)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (consultation is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No consultation job order found",
                IsSuccess = true
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Record found",
            Response = consultation.Adapt<DoctorConsultationResponse>()
        };
    }
}