using HealthEssentials.Core.DataAccess.Query.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Consultation;

public class GetConsultationEntityGroupListHandler : QueryBaseHandler, IRequestHandler<GetConsultationEntityGroupListQuery, QueryResponse<List<ConsultationEntityGroupResponse>>>
{
    public GetConsultationEntityGroupListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<ConsultationEntityGroupResponse>>> Handle(GetConsultationEntityGroupListQuery request, CancellationToken cancellationToken)
    {
        var consultationEntityGroup = await _dataLayer.HealthEssentialsContext.ConsultationEntityGroups
            .Where(x => EF.Functions.Like(x.Name, $"%{request.SearchField}%"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!consultationEntityGroup.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Consultation Entity Group Found",
                IsSuccess = true
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "No Consultation Entity Group Found",
            IsSuccess = true,
            Response = consultationEntityGroup.Adapt<List<ConsultationEntityGroupResponse>>()
        };
    }
}