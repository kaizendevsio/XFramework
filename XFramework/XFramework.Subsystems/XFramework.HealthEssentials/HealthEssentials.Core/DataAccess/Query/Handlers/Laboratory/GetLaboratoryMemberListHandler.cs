using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryMemberListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryMemberListQuery, QueryResponse<List<LaboratoryMemberResponse>>>
{
    public GetLaboratoryMemberListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<QueryResponse<List<LaboratoryMemberResponse>>> Handle(GetLaboratoryMemberListQuery request, CancellationToken cancellationToken)
    {
        var laboratoryMember = await _dataLayer.HealthEssentialsContext.LaboratoryMembers
            .Where(i => EF.Functions.ILike(i.Name, $"%{request.SearchField}%"))
            .OrderBy(i => i.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        if (!laboratoryMember.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Laboratory Member Found",
                
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Laboratory Member Found",
            
            Response = laboratoryMember.Adapt<List<LaboratoryMemberResponse>>()
        };
    }
}