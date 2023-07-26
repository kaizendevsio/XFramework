using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyServiceHandler : QueryBaseHandler, IRequestHandler<GetPharmacyServiceQuery, QueryResponse<PharmacyServiceResponse>>
{
    public GetPharmacyServiceHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<PharmacyServiceResponse>> Handle(GetPharmacyServiceQuery request, CancellationToken cancellationToken)
    {
        var service = await _dataLayer.HealthEssentialsContext.PharmacyServices
            .Include(x => x.Entity)
            .ThenInclude(x => x.Group)
            .Include(x => x.PharmacyLocation)
            .ThenInclude(x => x.Pharmacy)
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
            Response = service.Adapt<PharmacyServiceResponse>()
        };
    }
}