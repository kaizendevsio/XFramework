using HealthEssentials.Core.DataAccess.Query.Entity.Medicine;
using HealthEssentials.Domain.Generics.Contracts.Responses.Medicine;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Medicine;

public class GetMedicineEntityListHandler : QueryBaseHandler, IRequestHandler<GetMedicineEntityListQuery, QueryResponse<List<MedicineEntityResponse>>>
{
    public GetMedicineEntityListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<MedicineEntityResponse>>> Handle(GetMedicineEntityListQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.HealthEssentialsContext.MedicineEntities
            .Include(x => x.Group)
            .Where(x => EF.Functions.ILike(x.Name, $"%{request.PageSize}%"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!entity.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No data found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Data found",
            IsSuccess = true,
            Response = entity.Adapt<List<MedicineEntityResponse>>()
        };
    }
}