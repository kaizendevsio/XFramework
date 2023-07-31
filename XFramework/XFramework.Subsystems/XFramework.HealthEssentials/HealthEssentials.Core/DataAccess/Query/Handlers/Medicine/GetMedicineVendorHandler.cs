using HealthEssentials.Core.DataAccess.Query.Entity.Medicine;
using HealthEssentials.Domain.Generics.Contracts.Responses.Medicine;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Medicine;

public class GetMedicineVendorHandler : QueryBaseHandler, IRequestHandler<GetMedicineVendorQuery, QueryResponse<MedicineVendorResponse>>
{
    public GetMedicineVendorHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<MedicineVendorResponse>> Handle(GetMedicineVendorQuery request, CancellationToken cancellationToken)
    {
        var vendor = await _dataLayer.HealthEssentialsContext.MedicineVendors
            .Include(x => x.Medicine)
            .Include(x => x.Vendor)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (vendor is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No record found",
                
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Record found",
            Response = vendor.Adapt<MedicineVendorResponse>()
        };
    }
}