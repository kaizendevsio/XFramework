using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryJobOrderHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryJobOrderQuery, QueryResponse<LaboratoryJobOrderResponse>>
{
    public GetLaboratoryJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<LaboratoryJobOrderResponse>> Handle(GetLaboratoryJobOrderQuery request, CancellationToken cancellationToken)
    {
        var laboratoryJobOrders = await _dataLayer.HealthEssentialsContext.LaboratoryJobOrders
            .Include(i => i.ConsultationJobOrder)
            .Include(x => x.Laboratory)
            .Include(x => x.LaboratoryLocation)
            .Include(x => x.Patient)
            .Include(x => x.Schedule)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}" ,CancellationToken.None);

        if (laboratoryJobOrders is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Laboratory Job Order Found",
                IsSuccess = true
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Laboratory Job Order Found",
            Response = laboratoryJobOrders.Adapt<LaboratoryJobOrderResponse>()
        };
    }
}