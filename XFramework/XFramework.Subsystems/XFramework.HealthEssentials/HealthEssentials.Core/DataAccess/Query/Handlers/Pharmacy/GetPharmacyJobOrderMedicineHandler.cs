using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyJobOrderMedicineHandler : QueryBaseHandler, IRequestHandler<GetPharmacyJobOrderMedicineQuery, QueryResponse<PharmacyJobOrderMedicineResponse>>
{
    public GetPharmacyJobOrderMedicineHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<PharmacyJobOrderMedicineResponse>> Handle(GetPharmacyJobOrderMedicineQuery request, CancellationToken cancellationToken)
    {
        var jobOrderMedicine = await _dataLayer.HealthEssentialsContext.PharmacyJobOrderMedicines
            .Include(x => x.PharmacyJobOrder)
            .Include(x => x.Medicine)
            .Include(x => x.MedicineIntake)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (jobOrderMedicine is null)
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
            Response = jobOrderMedicine.Adapt<PharmacyJobOrderMedicineResponse>()
        };
    }
}