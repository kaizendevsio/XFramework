using HealthEssentials.Core.DataAccess.Query.Entity.Medicine;
using HealthEssentials.Domain.Generics.Contracts.Responses.Medicine;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Medicine;

public class GetMedicineTagListHandler : QueryBaseHandler, IRequestHandler<GetMedicineTagListQuery, QueryResponse<List<MedicineTagResponse>>>
{
    public GetMedicineTagListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<MedicineTagResponse>>> Handle(GetMedicineTagListQuery request, CancellationToken cancellationToken)
    {
        var tag = await _dataLayer.HealthEssentialsContext.MedicineTags
            .Include(x => x.Tag)
            .Include(x => x.Medicine)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!tag.Any())
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
            Response = tag.Adapt<List<MedicineTagResponse>>()
        };
    }
}