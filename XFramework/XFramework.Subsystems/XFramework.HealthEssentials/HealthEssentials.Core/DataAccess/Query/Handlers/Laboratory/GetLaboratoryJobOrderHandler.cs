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
        var laboratory = await _dataLayer.HealthEssentialsContext.LaboratoryJobOrders
            .Include(i => i.ConsultationJobOrder.Consultation)
            .Include(i => i.LaboratoryJobOrderResults)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}" ,CancellationToken.None);

        if (laboratory is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "Laboratory Job Order not found",
                IsSuccess = true
            };
        }
        
        var response = laboratory.Adapt<LaboratoryJobOrderResponse>();
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Laboratory Job Order found",
            IsSuccess = true,
            Response = response
        };
    }
}