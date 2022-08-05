using HealthEssentials.Core.DataAccess.Query.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Consultation;

public class GetConsultationJobOrderHandler : QueryBaseHandler, IRequestHandler<GetConsultationJobOrderQuery, QueryResponse<ConsultationJobOrderResponse>>
{
    public GetConsultationJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<ConsultationJobOrderResponse>> Handle(GetConsultationJobOrderQuery request, CancellationToken cancellationToken)
    {
        var consultationJobOrder = await _dataLayer.HealthEssentialsContext.ConsultationJobOrders
            .Include(x => x.Consultation)
            .Include(x => x.Schedule)
            .Include(x => x.DoctorConsultationJobOrders)
            .ThenInclude(x => x.Doctor)
            .Include(x => x.PatientConsultations)
            .ThenInclude(x => x.Patient)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);

        if (consultationJobOrder is null)
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
            Response = consultationJobOrder.Adapt<ConsultationJobOrderResponse>()
        };
    }
}