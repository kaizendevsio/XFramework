using HealthEssentials.Core.DataAccess.Query.Entity.Ailment;
using HealthEssentials.Domain.Generics.Contracts.Responses.Ailment;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Ailment;

public class GetAilmentTagListHandler : QueryBaseHandler, IRequestHandler<GetAilmentTagListQuery, QueryResponse<List<AilmentTagResponse>>>
{
    public GetAilmentTagListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<AilmentTagResponse>>> Handle(GetAilmentTagListQuery request, CancellationToken cancellationToken)
    {
        var ailmentTag = await _dataLayer.HealthEssentialsContext.AilmentTags
            .Include(x => x.Ailment)
            .Include(x => x.Tag)
            .OrderBy(x => x.CreatedAt)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        if (!ailmentTag.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Ailment Tag Found",
                
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Ailment Tag Found",
            
            Response = ailmentTag.Adapt<List<AilmentTagResponse>>()
        };
    }
}