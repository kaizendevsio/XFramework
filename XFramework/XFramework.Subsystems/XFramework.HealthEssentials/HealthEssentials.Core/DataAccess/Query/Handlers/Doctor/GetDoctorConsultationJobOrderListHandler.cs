using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;

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
            .Include(x => x.ConsultationJobOrder)
            .Include(x => x.Doctor)
            .Where(i => i.ConsultationJobOrder.Status == (int)request.Status)
            .Where(i => EF.Functions.ILike(i.ConsultationJobOrder.Remarks, $"%{request.SearchField}%"))
            .Where(i => EF.Functions.ILike(i.ConsultationJobOrder.ReferenceNumber, $"%{request.SearchField}%"))
            .Where(i => EF.Functions.ILike(i.ConsultationJobOrder.Prescription, $"%{request.SearchField}%"))
            .Where(i => EF.Functions.ILike(i.ConsultationJobOrder.Diagnosis, $"%{request.SearchField}%"))
            .Where(i => EF.Functions.ILike(i.ConsultationJobOrder.Treatment, $"%{request.SearchField}%"))
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