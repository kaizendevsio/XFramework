using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryListQuery, QueryResponse<List<LaboratoryResponse>>>
{
    public GetLaboratoryListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<LaboratoryResponse>>> Handle(GetLaboratoryListQuery request, CancellationToken cancellationToken)
    {
        var Laboratory = await _dataLayer.HealthEssentialsContext.Laboratories
            .AsNoTracking()
            .Where(i => EF.Functions.Like(i.Name, $"%{request.SearchField}%"))
            .OrderBy(i => i.Name)
            .Take(request.PageSize)
            .ToListAsync(CancellationToken.None);

        if (!Laboratory.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Laboratory Found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Laboratory Found",
            IsSuccess = true,
            Response = Laboratory.Adapt<List<LaboratoryResponse>>()
        };
    }
}