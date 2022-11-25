using HealthEssentials.Core.DataAccess.Query.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Consultation;

public class GetConsultationJobOrderLaboratoryListHandler : QueryBaseHandler, IRequestHandler<GetConsultationJobOrderLaboratoryListQuery, QueryResponse<List<ConsultationJobOrderLaboratoryResponse>>>
{
    public GetConsultationJobOrderLaboratoryListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<ConsultationJobOrderLaboratoryResponse>>> Handle(GetConsultationJobOrderLaboratoryListQuery request, CancellationToken cancellationToken)
    {
        var consultationJobOrderLaboratory = await _dataLayer.HealthEssentialsContext.ConsultationJobOrderLaboratories
            .Include(x => x.ConsultationJobOrder)
            .Include(x => x.LaboratoryService)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!consultationJobOrderLaboratory.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No consultation job order laboratory found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Consultation job order laboratory found",
            IsSuccess = true,
            Response = consultationJobOrderLaboratory.Adapt<List<ConsultationJobOrderLaboratoryResponse>>()
        };
    }
}