using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Responses.Storage;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Logistic;

public class GetLogisticRiderHandler : QueryBaseHandler, IRequestHandler<GetLogisticRiderQuery, QueryResponse<LogisticRiderResponse>>
{
    public GetLogisticRiderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<LogisticRiderResponse>> Handle(GetLogisticRiderQuery request, CancellationToken cancellationToken)
    {
        var rider = await _dataLayer.HealthEssentialsContext.LogisticRiders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (rider is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No record found",
                
            };
        }

        var response = rider.Adapt<LogisticRiderResponse>();
        response.Files = _dataLayer.XnelSystemsContext.StorageFiles
            .Where(i => i.IdentifierGuid == $"{response.Guid}")
            .AsNoTracking()
            .ToList()
            .Adapt<List<StorageFileResponse>>();
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Rider found",
            
            Response = response
        };        
    }
    
}