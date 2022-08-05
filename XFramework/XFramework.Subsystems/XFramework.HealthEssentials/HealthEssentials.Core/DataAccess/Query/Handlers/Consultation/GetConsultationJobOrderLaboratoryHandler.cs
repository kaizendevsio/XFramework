using HealthEssentials.Core.DataAccess.Query.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Consultation;

public class GetConsultationJobOrderLaboratoryHandler : QueryBaseHandler, IRequestHandler<GetConsultationJobOrderLaboratoryQuery, QueryResponse<ConsultationJobOrderLaboratoryResponse>>
{
    public GetConsultationJobOrderLaboratoryHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<ConsultationJobOrderLaboratoryResponse>> Handle(GetConsultationJobOrderLaboratoryQuery request, CancellationToken cancellationToken)
    {
        var jobOrderLaboratory = await _dataLayer.HealthEssentialsContext.ConsultationJobOrderLaboratories
            .Include(x => x.ConsultationJobOrder)
            .Include(x => x.LaboratoryService)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (jobOrderLaboratory is null)
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
            Response = jobOrderLaboratory.Adapt<ConsultationJobOrderLaboratoryResponse>()
        };
    }
}