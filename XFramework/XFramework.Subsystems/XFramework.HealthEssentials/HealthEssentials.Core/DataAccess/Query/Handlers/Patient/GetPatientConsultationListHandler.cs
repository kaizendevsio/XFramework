using HealthEssentials.Core.DataAccess.Query.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Patient;

public class GetPatientConsultationListHandler : QueryBaseHandler, IRequestHandler<GetPatientConsultationListQuery, QueryResponse<List<PatientConsultationResponse>>>
{
    public GetPatientConsultationListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<PatientConsultationResponse>>> Handle(GetPatientConsultationListQuery request, CancellationToken cancellationToken)
    {
        var patientConsultation = await _dataLayer.HealthEssentialsContext.PatientConsultations
            //.Include(x => x.ConsultationJobOrder.DoctorConsultationJobOrders)
            //.ThenInclude(i => i.Doctor)
            //.Include(x => x.Patient)
            //.Where(x => x.ConsultationJobOrder.Status == (int)request.Status)
            .Where(x => EF.Functions.ILike(x.ConsultationJobOrder.Remarks, $"%{request.SearchField}%"))
            .Where(x => EF.Functions.ILike(x.ConsultationJobOrder.Diagnosis, $"%{request.SearchField}%"))
            .Where(x => EF.Functions.ILike(x.ConsultationJobOrder.Prescription, $"%{request.SearchField}%"))
            .Where(x => EF.Functions.ILike(x.ConsultationJobOrder.Symptoms, $"%{request.SearchField}%"))
            .Where(x => EF.Functions.ILike(x.ConsultationJobOrder.Treatment, $"%{request.SearchField}%"))
            .Where(x => EF.Functions.ILike(x.ConsultationJobOrder.ReferenceNumber, $"%{request.SearchField}%"))
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!patientConsultation.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No patient consultation found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Patient consultation found",
            IsSuccess = true,
            Response = patientConsultation.Adapt<List<PatientConsultationResponse>>()
        };
    }
}