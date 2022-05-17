using HealthEssentials.Core.DataAccess.Query.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Consultation;

public class GetConsultationListHandler : QueryBaseHandler, IRequestHandler<GetConsultationListQuery, QueryResponse<List<ConsultationResponse>>>
{
    public GetConsultationListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<ConsultationResponse>>> Handle(GetConsultationListQuery request, CancellationToken cancellationToken)
    {
        var consultation = await _dataLayer.HealthEssentialsContext.Consultations
            .AsNoTracking()
            .Where(i => EF.Functions.Like(i.Name, $"%{request.SearchField}%"))
            .Take(request.PageSize)
            .OrderBy(i => i.Name)
            .ToListAsync(CancellationToken.None);

        if (!consultation.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Consultation Found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Consultation Found",
            IsSuccess = true,
            Response = consultation.Adapt<List<ConsultationResponse>>()
        };
    }
}