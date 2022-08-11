using HealthEssentials.Core.DataAccess.Query.Entity.Medicine;
using HealthEssentials.Domain.Generics.Contracts.Responses.Medicine;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Medicine;

public class GetMedicineIntakeHandler : QueryBaseHandler, IRequestHandler<GetMedicineIntakeQuery, QueryResponse<MedicineIntakeResponse>>
{
    public GetMedicineIntakeHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<MedicineIntakeResponse>> Handle(GetMedicineIntakeQuery request, CancellationToken cancellationToken)
    {
        var intake = await _dataLayer.HealthEssentialsContext.MedicineIntakes
            .Include(x => x.Entity)
            .Include(x => x.Unit)
            .AsSplitQuery()
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
            Response = intake.Adapt<MedicineIntakeResponse>()
        };
    }
}