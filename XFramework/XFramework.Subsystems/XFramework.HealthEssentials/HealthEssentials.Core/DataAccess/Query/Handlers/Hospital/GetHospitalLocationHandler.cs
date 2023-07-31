using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalLocationHandler : QueryBaseHandler, IRequestHandler<GetHospitalLocationQuery, QueryResponse<HospitalLocationResponse>>
{
    public GetHospitalLocationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<HospitalLocationResponse>> Handle(GetHospitalLocationQuery request, CancellationToken cancellationToken)
    {
        var location = await _dataLayer.HealthEssentialsContext.HospitalLocations
            .Include(x => x.Hospital)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (location is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No record found",
                
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Record found",
            Response = location.Adapt<HospitalLocationResponse>()
        };
    }
}