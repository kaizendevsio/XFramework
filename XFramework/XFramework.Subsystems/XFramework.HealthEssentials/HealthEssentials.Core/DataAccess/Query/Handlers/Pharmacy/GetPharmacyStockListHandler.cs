using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyStockListHandler : QueryBaseHandler, IRequestHandler<GetPharmacyStockListQuery, QueryResponse<List<PharmacyStockResponse>>>
{
    public GetPharmacyStockListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PharmacyStockResponse>>> Handle(GetPharmacyStockListQuery request, CancellationToken cancellationToken)
    {
        var stock = await _dataLayer.HealthEssentialsContext.PharmacyStocks
            .Include(x => x.Pharmacy)
            .Include(x => x.Medicine)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!stock.Any())
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
            Message = "Data found",
            
            Response = stock.Adapt<List<PharmacyStockResponse>>()
        };
    }
}