using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryEntityGroupHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryEntityGroupQuery, QueryResponse<LaboratoryEntityGroupResponse>>
{
    public GetLaboratoryEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<LaboratoryEntityGroupResponse>> Handle(GetLaboratoryEntityGroupQuery request, CancellationToken cancellationToken)
    {
        var group = await _dataLayer.HealthEssentialsContext.LaboratoryEntityGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (group is null)
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
            Response = group.Adapt<LaboratoryEntityGroupResponse>()
        };
    }
}