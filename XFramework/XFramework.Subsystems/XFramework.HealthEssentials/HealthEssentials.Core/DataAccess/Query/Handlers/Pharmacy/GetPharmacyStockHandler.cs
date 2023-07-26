using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyStockHandler : QueryBaseHandler, IRequestHandler<GetPharmacyStockQuery, QueryResponse<PharmacyStockResponse>>
{
    public GetPharmacyStockHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<PharmacyStockResponse>> Handle(GetPharmacyStockQuery request, CancellationToken cancellationToken)
    {
        var stock = await _dataLayer.HealthEssentialsContext.PharmacyStocks
            .Include(x => x.Pharmacy)
            .Include(x => x.Medicine)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
            
        if (stock is null)
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
            Response = stock.Adapt<PharmacyStockResponse>()
        };
    }
}