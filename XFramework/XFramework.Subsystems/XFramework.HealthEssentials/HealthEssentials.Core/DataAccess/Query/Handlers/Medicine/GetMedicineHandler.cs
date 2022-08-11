using HealthEssentials.Core.DataAccess.Query.Entity.Medicine;
using HealthEssentials.Domain.Generics.Contracts.Responses.Medicine;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Medicine;

public class GetMedicineHandler : QueryBaseHandler, IRequestHandler<GetMedicineQuery, QueryResponse<MedicineResponse>>
{
    public GetMedicineHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<MedicineResponse>> Handle(GetMedicineQuery request, CancellationToken cancellationToken)
    {
        var medicine = await _dataLayer.HealthEssentialsContext.Medicines
            .Include(x => x.Entity)
            .ThenInclude(x => x.Group)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (medicine is null)
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
            Response = medicine.Adapt<MedicineResponse>()
        };
    }
}