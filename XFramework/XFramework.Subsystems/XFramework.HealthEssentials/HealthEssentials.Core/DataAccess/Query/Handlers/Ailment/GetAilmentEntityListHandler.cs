using HealthEssentials.Core.DataAccess.Query.Entity.Ailment;
using HealthEssentials.Domain.Generics.Contracts.Responses.Ailment;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Ailment;

public class GetAilmentEntityListHandler : QueryBaseHandler, IRequestHandler<GetAilmentEntityListQuery, QueryResponse<List<AilmentEntityResponse>>>
{
    public GetAilmentEntityListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<AilmentEntityResponse>>> Handle(GetAilmentEntityListQuery request, CancellationToken cancellationToken)
    {
        var ailmentEntity = await _dataLayer.HealthEssentialsContext.AilmentEntities
            .Include(x => x.Group)
            .Where(x => EF.Functions.Like(x.Name, $"%{request.SearchField}%"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        if (!ailmentEntity.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Ailment Entity  Found",
                IsSuccess = true
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Ailment Entity Found",
            IsSuccess = true,
            Response = ailmentEntity.Adapt<List<AilmentEntityResponse>>()
        };
    }
}