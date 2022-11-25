using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Tag;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalEntityGroupListHandler : QueryBaseHandler, IRequestHandler<GetHospitalEntityGroupListQuery, QueryResponse<List<HospitalEntityGroupResponse>>>
{
    public GetHospitalEntityGroupListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<HospitalEntityGroupResponse>>> Handle(GetHospitalEntityGroupListQuery request, CancellationToken cancellationToken)
    {
        var entityGroup = await _dataLayer.HealthEssentialsContext.HospitalEntityGroups
            .Where(x => EF.Functions.ILike(x.Name, $"%{request.SearchField}%"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        if (!entityGroup.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Hospital Entity Group Found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Hospital Entity Group Found",
            IsSuccess = true,
            Response = entityGroup.Adapt<List<HospitalEntityGroupResponse>>()
        };

    }
}