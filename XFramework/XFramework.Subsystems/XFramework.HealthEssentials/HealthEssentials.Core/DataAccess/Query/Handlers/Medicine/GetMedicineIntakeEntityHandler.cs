using HealthEssentials.Core.DataAccess.Query.Entity.Medicine;
using HealthEssentials.Domain.Generics.Contracts.Responses.Medicine;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Medicine;

public class GetMedicineIntakeEntityHandler : QueryBaseHandler, IRequestHandler<GetMedicineIntakeEntityQuery, QueryResponse<MedicineIntakeEntityResponse>>
{
    public GetMedicineIntakeEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<MedicineIntakeEntityResponse>> Handle(GetMedicineIntakeEntityQuery request, CancellationToken cancellationToken)
    {
        var intake = await _dataLayer.HealthEssentialsContext.MedicineIntakeEntities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (intake is null)
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
            Response = intake.Adapt<MedicineIntakeEntityResponse>()
        };
    }
}