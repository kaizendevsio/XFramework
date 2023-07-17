using HealthEssentials.Core.DataAccess.Query.Entity.Ailment;
using HealthEssentials.Domain.Generics.Contracts.Responses.Ailment;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Ailment;

public class GetAilmentListHandler : QueryBaseHandler, IRequestHandler<GetAilmentListQuery, QueryResponse<List<AilmentResponse>>>
{
    public GetAilmentListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<AilmentResponse>>> Handle(GetAilmentListQuery request, CancellationToken cancellationToken)
    {
        var ailment = await _dataLayer.HealthEssentialsContext.Ailments
            .Include(x => x.Entity)
            .ThenInclude(x => x.Group)
            .Where(x => EF.Functions.ILike(x.Name, $"%{request.SearchField}%"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        if (!ailment.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Ailment Found",
                
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Ailment Found",
            
            Response = ailment.Adapt<List<AilmentResponse>>()
        };
    }
}