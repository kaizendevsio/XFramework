using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryLocationTagHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryLocationTagQuery, QueryResponse<LaboratoryLocationTagResponse>>
{
    public GetLaboratoryLocationTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<LaboratoryLocationTagResponse>> Handle(GetLaboratoryLocationTagQuery request, CancellationToken cancellationToken)
    {
        var locationTag = await _dataLayer.HealthEssentialsContext.LaboratoryLocationTags
            .Include(x => x.LaboratoryLocation)
            .ThenInclude(x => x.Laboratory)
            .Include(x => x.Tag)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (locationTag is null)
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
            Response = locationTag.Adapt<LaboratoryLocationTagResponse>()
        };
    }
}