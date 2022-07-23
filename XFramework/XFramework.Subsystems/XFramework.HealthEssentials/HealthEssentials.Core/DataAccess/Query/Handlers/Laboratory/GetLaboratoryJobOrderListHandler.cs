using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;
using HealthEssentials.Domain.DataTransferObjects;
using HealthEssentials.Domain.Generics.Contracts.Responses.Storage;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryJobOrderListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryJobOrderListQuery, QueryResponse<List<LaboratoryJobOrderResponse>>>
{
    public GetLaboratoryJobOrderListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<LaboratoryJobOrderResponse>>> Handle(GetLaboratoryJobOrderListQuery request, CancellationToken cancellationToken)
    {
        var laboratory = await _dataLayer.HealthEssentialsContext.LaboratoryJobOrders
            .Include(i => i.ConsultationJobOrder)
            .Where(i => EF.Functions.ILike(i.ReferenceNumber, $"%{request.SearchField}%"))
            .Where(i => i.Status == (int) request.Status)
            .Where(i => i.LaboratoryLocation.Guid == $"{request.LaboratoryLocationGuid}")
            .OrderBy(i => i.CreatedAt)
            .Take(request.PageSize)
            .AsNoTracking()
            .AsSplitQuery()
            .ToListAsync(CancellationToken.None);

        if (!laboratory.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No records found",
                IsSuccess = true
            };
        }
        
        var response = laboratory.Adapt<List<LaboratoryJobOrderResponse>>();
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Job Orders Found",
            IsSuccess = true,
            Response = response
        };
    }
}