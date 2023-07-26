using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyJobOrderConsultationJobOrderListHandler : QueryBaseHandler, IRequestHandler<GetPharmacyJobOrderConsultationJobOrderListQuery, QueryResponse<List<PharmacyJobOrderConsultationJobOrderResponse>>>
{
    public GetPharmacyJobOrderConsultationJobOrderListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PharmacyJobOrderConsultationJobOrderResponse>>> Handle(GetPharmacyJobOrderConsultationJobOrderListQuery request, CancellationToken cancellationToken)
    {
        var consultationJobOrder = await _dataLayer.HealthEssentialsContext.PharmacyJobOrderConsultationJobOrders
            .Include(x => x.ConsultationJobOrder)
            .Include(x => x.PharmacyJobOrder)
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
                
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "No consultation job order found",
            
            Response = consultationJobOrder.Adapt<List<PharmacyJobOrderConsultationJobOrderResponse>>()
        };
    }
}