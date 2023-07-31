using HealthEssentials.Core.DataAccess.Query.Entity.Medicine;
using HealthEssentials.Domain.Generics.Contracts.Responses.Medicine;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Medicine;

public class GetMedicineListHandler : QueryBaseHandler, IRequestHandler<GetMedicineListQuery, QueryResponse<List<MedicineResponse>>>
{
    public GetMedicineListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<MedicineResponse>>> Handle(GetMedicineListQuery request,
        CancellationToken cancellationToken)
    {
        var medicine = await _dataLayer.HealthEssentialsContext.Medicines
            .Include(x => x.Entity)
            .ThenInclude(x => x.Group)
            .Where(x => EF.Functions.ILike(x.Name, $"%{request.SearchField}%"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        if (!medicine.Any())
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
            
            Response = medicine.Adapt<List<MedicineResponse>>()
        };
    }
}