using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyJobOrderMedicineListHandler : QueryBaseHandler, IRequestHandler<GetPharmacyJobOrderMedicineListQuery, QueryResponse<List<PharmacyJobOrderMedicineResponse>>>
{
    public GetPharmacyJobOrderMedicineListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PharmacyJobOrderMedicineResponse>>> Handle(GetPharmacyJobOrderMedicineListQuery request, CancellationToken cancellationToken)
    {
        var jobOrderMedicine = await _dataLayer.HealthEssentialsContext.PharmacyJobOrderMedicines
            .Include(x => x.PharmacyJobOrder)
            .Include(x => x.Medicine)
            .Include(x => x.MedicineIntake)
            .Where(x => x.Status == (int) request.Status)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!jobOrderMedicine.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No records found",
                
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Records found",
            
            Response = jobOrderMedicine.Adapt<List<PharmacyJobOrderMedicineResponse>>()
        };
    }
}