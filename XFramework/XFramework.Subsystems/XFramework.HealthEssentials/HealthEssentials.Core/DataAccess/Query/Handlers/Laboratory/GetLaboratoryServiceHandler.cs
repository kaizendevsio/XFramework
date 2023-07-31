using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryServiceHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryServiceQuery, QueryResponse<LaboratoryServiceResponse>>
{
    public GetLaboratoryServiceHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<LaboratoryServiceResponse>> Handle(GetLaboratoryServiceQuery request, CancellationToken cancellationToken)
    {
        var service = await _dataLayer.HealthEssentialsContext.LaboratoryServices
            .Include(x => x.Entity)
            .ThenInclude(x => x.Group)
            .Include(x => x.LaboratoryLocation)
            .ThenInclude(x => x.Laboratory)
            .Include(x => x.Unit)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (service is null)
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
            Response = service.Adapt<LaboratoryServiceResponse>()
        };

    }
}