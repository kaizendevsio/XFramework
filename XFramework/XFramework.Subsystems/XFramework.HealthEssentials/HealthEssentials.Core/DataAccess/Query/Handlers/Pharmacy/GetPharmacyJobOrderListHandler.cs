using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyJobOrderListHandler : QueryBaseHandler, IRequestHandler<GetPharmacyJobOrderListQuery, QueryResponse<List<PharmacyJobOrderResponse>>>
{
    public GetPharmacyJobOrderListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PharmacyJobOrderResponse>>> Handle(GetPharmacyJobOrderListQuery request, CancellationToken cancellationToken)
    {
        var pharmacy = await _dataLayer.HealthEssentialsContext.PharmacyJobOrders
            .Include(i => i.PharmacyLocation)
            .Include(i => i.Schedule)
            .Where(i => EF.Functions.ILike(i.ReferenceNumber, $"%{request.SearchField}%"))
            .Where(i => i.Status == (int) request.RecordType)
            .OrderBy(i => i.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        if (!pharmacy.Any())
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
            IsSuccess = true,
            Response = pharmacy.Adapt<List<PharmacyJobOrderResponse>>()
        };
    }
}