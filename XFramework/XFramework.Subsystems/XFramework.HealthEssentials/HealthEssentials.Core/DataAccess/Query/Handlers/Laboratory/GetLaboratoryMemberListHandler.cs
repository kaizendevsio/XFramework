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
            .Where(i => EF.Functions.Like(i.Name, $"%{request.SearchField}%"))
            .OrderBy(i => i.Name)
            .Take(request.PageSize)
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        if (!laboratoryMember.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Laboratory Member Found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Laboratory Member Found",
            IsSuccess = true,
            Response = laboratoryMember.Adapt<List<LaboratoryMemberResponse>>()
        };
    }
}