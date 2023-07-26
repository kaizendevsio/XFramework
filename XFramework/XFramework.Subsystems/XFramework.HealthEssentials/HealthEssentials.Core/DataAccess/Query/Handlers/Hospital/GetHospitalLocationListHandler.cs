using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalLocationListHandler : QueryBaseHandler, IRequestHandler<GetHospitalLocationListQuery, QueryResponse<List<HospitalLocationResponse>>>
{
    public GetHospitalLocationListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<HospitalLocationResponse>>> Handle(GetHospitalLocationListQuery request, CancellationToken cancellationToken)
    {
        var location = await _dataLayer.HealthEssentialsContext.HospitalLocations
            .Include(x => x.Hospital)
            .Where(x => EF.Functions.ILike(x.Name, $"%{request.SearchField}%"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!location.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No hospital location found",
                
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Hospital location found",
            
            Response = location.Adapt<List<HospitalLocationResponse>>()
        };
    }
}