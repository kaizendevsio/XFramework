using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalListHandler : QueryBaseHandler, IRequestHandler<GetHospitalListQuery, QueryResponse<List<HospitalResponse>>>
{
    public GetHospitalListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<HospitalResponse>>> Handle(GetHospitalListQuery request, CancellationToken cancellationToken)
    {
        var hospital = await _dataLayer.HealthEssentialsContext.Hospitals
            .Include(x => x.Entity)
            .ThenInclude(x => x.Group)
            .Where(x => EF.Functions.ILike(x.Name, $"%{request.SearchField}%"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!hospital.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Hospital Found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Hospital Found",
            IsSuccess = true,
            Response = hospital.Adapt<List<HospitalResponse>>()
        };
    }
}