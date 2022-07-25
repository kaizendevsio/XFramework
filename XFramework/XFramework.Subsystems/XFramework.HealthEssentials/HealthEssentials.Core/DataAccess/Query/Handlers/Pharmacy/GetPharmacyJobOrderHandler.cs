using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyJobOrderHandler : QueryBaseHandler, IRequestHandler<GetPharmacyJobOrderQuery, QueryResponse<PharmacyJobOrderResponse>>
{
    public GetPharmacyJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<PharmacyJobOrderResponse>> Handle(GetPharmacyJobOrderQuery request, CancellationToken cancellationToken)
    {
        var jobOrder = await _dataLayer.HealthEssentialsContext.PharmacyJobOrders
            .Include(x => x.Patient)
            .Include(x => x.PharmacyLocation)
            .Include(x => x.Schedule)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (jobOrder is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No data found",
                IsSuccess = true
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Data Found",
            Response = jobOrder.Adapt<PharmacyJobOrderResponse>()
        };
    }
}