using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyJobOrderConsultationJobOrderHandler : QueryBaseHandler, IRequestHandler<GetPharmacyJobOrderConsultationJobOrderQuery, QueryResponse<PharmacyJobOrderConsultationJobOrderResponse>>
{
    public GetPharmacyJobOrderConsultationJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<PharmacyJobOrderConsultationJobOrderResponse>> Handle(GetPharmacyJobOrderConsultationJobOrderQuery request, CancellationToken cancellationToken)
    {
        var consultationJobOrder = await _dataLayer.HealthEssentialsContext.PharmacyJobOrderConsultationJobOrders
            .Include(x => x.ConsultationJobOrder)
            .Include(x => x.PharmacyJobOrder)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (consultationJobOrder is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No data found",
                
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Data Found",
            Response = consultationJobOrder.Adapt<PharmacyJobOrderConsultationJobOrderResponse>()
        };
    }
}