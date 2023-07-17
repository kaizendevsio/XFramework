using HealthEssentials.Core.DataAccess.Query.Entity.Medicine;
using HealthEssentials.Domain.Generics.Contracts.Responses.Medicine;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Medicine;

public class GetMedicineVendorListHandler : QueryBaseHandler, IRequestHandler<GetMedicineVendorListQuery, QueryResponse<List<MedicineVendorResponse>>>
{
    public GetMedicineVendorListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<MedicineVendorResponse>>> Handle(GetMedicineVendorListQuery request, CancellationToken cancellationToken)
    {
        var vendor = await _dataLayer.HealthEssentialsContext.MedicineVendors
            .Include(x => x.Vendor)
            .Include(x => x.Medicine)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!vendor.Any())
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
            
            Response = vendor.Adapt<List<MedicineVendorResponse>>()
        };
    }
}