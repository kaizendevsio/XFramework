using HealthEssentials.Core.DataAccess.Query.Entity.Medicine;
using HealthEssentials.Domain.Generics.Contracts.Responses.Medicine;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Medicine;

public class GetMedicineEntityGroupHandler : QueryBaseHandler, IRequestHandler<GetMedicineEntityGroupQuery, QueryResponse<MedicineEntityGroupResponse>>
{
    public GetMedicineEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<MedicineEntityGroupResponse>> Handle(GetMedicineEntityGroupQuery request, CancellationToken cancellationToken)
    {
        var group = await _dataLayer.HealthEssentialsContext.MedicineEntityGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (group is null)
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
            Response = group.Adapt<MedicineEntityGroupResponse>()
        };
    }
}