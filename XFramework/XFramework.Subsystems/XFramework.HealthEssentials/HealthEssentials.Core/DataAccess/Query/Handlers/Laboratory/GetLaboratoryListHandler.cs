using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;
using HealthEssentials.Domain.DataTransferObjects;
using HealthEssentials.Domain.Generics.Contracts.Responses.Storage;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryListQuery, QueryResponse<List<LaboratoryResponse>>>
{
    public GetLaboratoryListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<LaboratoryResponse>>> Handle(GetLaboratoryListQuery request, CancellationToken cancellationToken)
    {
        var laboratory = await _dataLayer.HealthEssentialsContext.Laboratories
            .AsNoTracking()
            .Where(i => EF.Functions.ILike(i.Name, $"%{request.SearchField}%"))
            .Where(i => i.Status == (int) request.Status)
            .OrderBy(i => i.Name)
            .Take(request.PageSize)
            .ToListAsync(CancellationToken.None);

        if (!laboratory.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Laboratory Found",
                IsSuccess = true
            };
        }
        
        var response = laboratory.Adapt<List<LaboratoryResponse>>();
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Laboratory Found",
            IsSuccess = true,
            Response = response
        };
    }
}