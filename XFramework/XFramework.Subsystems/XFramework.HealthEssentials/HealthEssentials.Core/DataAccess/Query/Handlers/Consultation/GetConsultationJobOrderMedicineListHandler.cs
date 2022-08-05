using HealthEssentials.Core.DataAccess.Query.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Consultation;

public class GetConsultationJobOrderMedicineListHandler : QueryBaseHandler, IRequestHandler<GetConsultationJobOrderMedicineListQuery, QueryResponse<List<ConsultationJobOrderMedicineResponse>>>
{
    public GetConsultationJobOrderMedicineListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<ConsultationJobOrderMedicineResponse>>> Handle(GetConsultationJobOrderMedicineListQuery request, CancellationToken cancellationToken)
    {
        var consultationJobOrderMedicine = await _dataLayer.HealthEssentialsContext.ConsultationJobOrderMedicines
            .Include(x => x.ConsultationJobOrder)
            .Include(x => x.Medicine)
            .Include(x => x.MedicineIntake)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!consultationJobOrderMedicine.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No consultation job order medicine found",
                IsSuccess = true
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "No consultation job order medicine found",
            IsSuccess = true,
            Response = consultationJobOrderMedicine.Adapt<List<ConsultationJobOrderMedicineResponse>>()
        };

    }
}