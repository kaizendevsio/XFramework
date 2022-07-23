using HealthEssentials.Core.DataAccess.Query.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Consultation;

public class GetConsultationJobOrderListHandler : QueryBaseHandler, IRequestHandler<GetConsultationJobOrderListQuery, QueryResponse<List<ConsultationJobOrderResponse>>>
{
    public GetConsultationJobOrderListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<ConsultationJobOrderResponse>>> Handle(GetConsultationJobOrderListQuery request, CancellationToken cancellationToken)
    {
        var consultationJobOrder = await _dataLayer.HealthEssentialsContext.ConsultationJobOrders
            .Include(x => x.Consultation)
            .Include(x => x.Schedule)
            .Include(x => x.PatientConsultations)
            .ThenInclude(x => x.Patient)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!consultationJobOrder.Any())
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
            Message = "No consultation job order found",
            IsSuccess = true,
            Response = consultationJobOrder.Adapt<List<ConsultationJobOrderResponse>>()
        };
    }
}