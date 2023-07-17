using HealthEssentials.Core.DataAccess.Query.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Consultation;

public class GetConsultationJobOrderMedicineHandler : QueryBaseHandler, IRequestHandler<GetConsultationJobOrderMedicineQuery, QueryResponse<ConsultationJobOrderMedicineResponse>>
{
    public GetConsultationJobOrderMedicineHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<ConsultationJobOrderMedicineResponse>> Handle(GetConsultationJobOrderMedicineQuery request, CancellationToken cancellationToken)
    {
        var jobOrderMedicine = await _dataLayer.HealthEssentialsContext.ConsultationJobOrderMedicines
            .Include(x => x.ConsultationJobOrder)
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
                
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Data Found",
            Response = jobOrderMedicine.Adapt<ConsultationJobOrderMedicineResponse>()
        };
    }
}