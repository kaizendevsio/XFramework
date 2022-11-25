using HealthEssentials.Core.DataAccess.Query.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Consultation;

public class GetConsultationTagListHandler : QueryBaseHandler, IRequestHandler<GetConsultationTagListQuery, QueryResponse<List<ConsultationTagResponse>>>
{
    public GetConsultationTagListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<ConsultationTagResponse>>> Handle(GetConsultationTagListQuery request, CancellationToken cancellationToken)
    {
        var consultationTag = await _dataLayer.HealthEssentialsContext.ConsultationTags
            .Include(x => x.Consultation)
            .Include(x => x.Tag)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!consultationTag.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No consultation tag found",
                IsSuccess = true
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "No consultation tag found",
            IsSuccess = true,
            Response = consultationTag.Adapt<List<ConsultationTagResponse>>()
        };
    }
}