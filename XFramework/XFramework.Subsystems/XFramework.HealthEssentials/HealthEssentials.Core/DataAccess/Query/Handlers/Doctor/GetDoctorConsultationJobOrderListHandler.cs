using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;
using HealthEssentials.Domain.Generics.Enums;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Doctor;

public class GetDoctorConsultationJobOrderListHandler : QueryBaseHandler, IRequestHandler<GetDoctorConsultationJobOrderListQuery, QueryResponse<List<DoctorConsultationJobOrderResponse>>>
{
    public GetDoctorConsultationJobOrderListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<DoctorConsultationJobOrderResponse>>> Handle(GetDoctorConsultationJobOrderListQuery request, CancellationToken cancellationToken)
    {
        var jobOrder = await _dataLayer.HealthEssentialsContext.DoctorConsultationJobOrders
            .Include(x => x.ConsultationJobOrder.PatientConsultations)
            .ThenInclude(i => i.Patient)
            .Include(x => x.Doctor)
            .Where(i => request.Status.Contains((TransactionRecordType)i.ConsultationJobOrder.Status))
            .Where(i => i.Doctor.Guid == $"{request.DoctorGuid}")
            .Where(i => EF.Functions.ILike(i.ConsultationJobOrder.Remarks, $"%{request.SearchField}%")
                        || EF.Functions.ILike(i.ConsultationJobOrder.ReferenceNumber, $"%{request.SearchField}%")
                        || EF.Functions.ILike(i.ConsultationJobOrder.Prescription, $"%{request.SearchField}%")
                        || EF.Functions.ILike(i.ConsultationJobOrder.Diagnosis, $"%{request.SearchField}%")
                        || EF.Functions.ILike(i.ConsultationJobOrder.Treatment, $"%{request.SearchField}%")
                        || EF.Functions.ILike(i.ConsultationJobOrder.Symptoms, $"%{request.SearchField}%"))
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!jobOrder.Any())
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
            Response = jobOrder.Adapt<List<DoctorConsultationJobOrderResponse>>()
        };
    }
}