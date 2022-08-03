using HealthEssentials.Core.DataAccess.Query.Entity.Medicine;
using HealthEssentials.Domain.Generics.Contracts.Responses.Medicine;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Medicine;

public class GetMedicineIntakeListHandler : QueryBaseHandler, IRequestHandler<GetMedicineIntakeListQuery, QueryResponse<List<MedicineIntakeResponse>>>
{
    public GetMedicineIntakeListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<MedicineIntakeResponse>>> Handle(GetMedicineIntakeListQuery request, CancellationToken cancellationToken)
    {
        var intake = await _dataLayer.HealthEssentialsContext.MedicineIntakes
            .Include(x => x.Entity)
            .Include(x => x.Unit)
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
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Data found",
            IsSuccess = true,
            Response = intake.Adapt<List<MedicineIntakeResponse>>()
        };

    }
}