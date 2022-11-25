using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalServiceListHandler : QueryBaseHandler, IRequestHandler<GetHospitalServiceListQuery, QueryResponse<List<HospitalServiceResponse>>>
{
    public GetHospitalServiceListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<HospitalServiceResponse>>> Handle(GetHospitalServiceListQuery request, CancellationToken cancellationToken)
    {
        var service = await _dataLayer.HealthEssentialsContext.HospitalServices
            .Include(x => x.Entity)
            .Include(x => x.Hospital)
            .ThenInclude(x => x.HospitalLocations)
            .Include(x => x.Unit)
            .Where(x => EF.Functions.ILike(x.Name, $"%{request.SearchField}%"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!service.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Hospital Service found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Hospital Service Found",
            IsSuccess = true,
            Response = service.Adapt<List<HospitalServiceResponse>>()
        };
    }
}