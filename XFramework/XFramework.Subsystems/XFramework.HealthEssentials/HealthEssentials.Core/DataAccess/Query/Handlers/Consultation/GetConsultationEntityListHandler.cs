using HealthEssentials.Core.DataAccess.Query.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Consultation;

public class GetConsultationEntityListHandler : QueryBaseHandler, IRequestHandler<GetConsultationEntityListQuery, QueryResponse<List<ConsultationEntityResponse>>>
{
    public GetConsultationEntityListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<ConsultationEntityResponse>>> Handle(GetConsultationEntityListQuery request, CancellationToken cancellationToken)
    {
        var consultationEntity = await _dataLayer.HealthEssentialsContext.ConsultationEntities
            .Include(i => i.Group)
            .Where(i => EF.Functions.ILike(i.Name, $"%{request.SearchField}%"))
            .OrderBy(i => i.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        
        if (!consultationEntity.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Consultation Entity Found",
                
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Consultation Entity Found",
            
            Response = consultationEntity.Adapt<List<ConsultationEntityResponse>>()
        };
    }
}