using HealthEssentials.Core.DataAccess.Query.Entity.Medicine;
using HealthEssentials.Domain.Generics.Contracts.Responses.Medicine;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Medicine;

public class GetMedicineIntakeEntityListHandler : QueryBaseHandler, IRequestHandler<GetMedicineIntakeEntityListQuery, QueryResponse<List<MedicineIntakeEntityResponse>>>
{
    public GetMedicineIntakeEntityListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<MedicineIntakeEntityResponse>>> Handle(GetMedicineIntakeEntityListQuery request, CancellationToken cancellationToken)
    {
        var intake = await _dataLayer.HealthEssentialsContext.MedicineIntakeEntities
            .Where(x => EF.Functions.ILike(x.Name, $"%{request.SearchField}%"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!intake.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No data found",
                
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Data found",
            
            Response = intake.Adapt<List<MedicineIntakeEntityResponse>>()
        };
    }
}