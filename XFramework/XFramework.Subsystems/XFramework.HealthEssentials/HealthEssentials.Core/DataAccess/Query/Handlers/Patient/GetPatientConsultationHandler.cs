using HealthEssentials.Core.DataAccess.Query.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Patient;

public class GetPatientConsultationHandler : QueryBaseHandler, IRequestHandler<GetPatientConsultationQuery, QueryResponse<PatientConsultationResponse>>
{
    public GetPatientConsultationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<PatientConsultationResponse>> Handle(GetPatientConsultationQuery request, CancellationToken cancellationToken)
    {
        var patientConsultation = await _dataLayer.HealthEssentialsContext.PatientConsultations
            .Include(x => x.ConsultationJobOrder)
            .Include(x => x.Patient)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);

        if (patientConsultation is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No consultation found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Record found",
            Response = patientConsultation.Adapt<PatientConsultationResponse>()
        };
    }
}