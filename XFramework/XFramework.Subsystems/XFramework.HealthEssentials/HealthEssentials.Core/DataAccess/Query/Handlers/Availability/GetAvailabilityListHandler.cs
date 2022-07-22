using HealthEssentials.Core.DataAccess.Query.Entity.Availability;
using HealthEssentials.Domain.Generics.Contracts.Responses.Availability;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Availability;

public class GetAvailabilityListHandler : QueryBaseHandler, IRequestHandler<GetAvailabilityListQuery, QueryResponse<List<AvailabilityResponse>>>
{
    public GetAvailabilityListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<AvailabilityResponse>>> Handle(GetAvailabilityListQuery request, CancellationToken cancellationToken)
    {
        var availability = await _dataLayer.HealthEssentialsContext.Availabilities
            .Include(x => x.Entity)
            .Where(x => EF.Functions.Like(x.Name, $"{request.SearchField}"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!availability.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Availability Found",
                IsSuccess = true
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Availability Found",
            IsSuccess = true,
            Response = availability.Adapt<List<AvailabilityResponse>>()
        };
    }
}