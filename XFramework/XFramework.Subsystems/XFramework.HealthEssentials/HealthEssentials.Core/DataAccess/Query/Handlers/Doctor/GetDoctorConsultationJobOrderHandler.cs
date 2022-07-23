using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Doctor;

public class GetDoctorConsultationJobOrderHandler : QueryBaseHandler, IRequestHandler<GetDoctorConsultationJobOrderQuery, QueryResponse<DoctorConsultationJobOrderResponse>>
{
    public GetDoctorConsultationJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<DoctorConsultationJobOrderResponse>> Handle(GetDoctorConsultationJobOrderQuery request, CancellationToken cancellationToken)
    {
        var jobOrder = await _dataLayer.HealthEssentialsContext.DoctorConsultationJobOrders
            .Include(x => x.ConsultationJobOrder.Consultation)
            .Include(x => x.Doctor)
            .OrderBy(x => x.CreatedAt)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}" ,CancellationToken.None);
        
        if (jobOrder is null)
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
            Response = jobOrder.Adapt<DoctorConsultationJobOrderResponse>()
        };
    }
}