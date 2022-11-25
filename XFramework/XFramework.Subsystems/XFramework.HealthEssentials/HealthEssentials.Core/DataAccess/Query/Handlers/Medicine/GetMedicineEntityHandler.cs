using HealthEssentials.Core.DataAccess.Query.Entity.Medicine;
using HealthEssentials.Domain.Generics.Contracts.Responses.Medicine;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Medicine;

public class GetMedicineEntityHandler : QueryBaseHandler, IRequestHandler<GetMedicineEntityQuery, QueryResponse<MedicineEntityResponse>>
{
    public GetMedicineEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<MedicineEntityResponse>> Handle(GetMedicineEntityQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.HealthEssentialsContext.MedicineEntities
            .Include(x => x.Group)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (entity is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No record found",
                IsSuccess = true
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Record found",
            Response = entity.Adapt<MedicineEntityResponse>()
        };
    }
}