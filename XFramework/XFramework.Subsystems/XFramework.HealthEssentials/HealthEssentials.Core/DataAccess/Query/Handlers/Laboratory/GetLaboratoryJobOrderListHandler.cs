using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;
using HealthEssentials.Domain.DataTransferObjects;
using HealthEssentials.Domain.Generics.Contracts.Responses.Storage;
using IdentityServer.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.Enums;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryJobOrderListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryJobOrderListQuery, QueryResponse<List<LaboratoryJobOrderResponse>>>
{
    public GetLaboratoryJobOrderListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<LaboratoryJobOrderResponse>>> Handle(GetLaboratoryJobOrderListQuery request, CancellationToken cancellationToken)
    {
        var laboratoryJobOrders = await _dataLayer.HealthEssentialsContext.LaboratoryJobOrders
            .Include(i => i.ConsultationJobOrder)
            .Include(x => x.Laboratory)
            .Include(x => x.LaboratoryLocation)
            .Include(x => x.Patient)
            .Include(x => x.Schedule)
            .Where(i => EF.Functions.ILike(i.ReferenceNumber, $"%{request.SearchField}%"))
            .Where(i => i.LaboratoryLocation.Guid == $"{request.LaboratoryLocationGuid}")
            .Where(i => request.Status.Contains((TransactionStatus)i.Status))
            .OrderBy(i => i.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        if (!laboratoryJobOrders.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No records found",
                IsSuccess = true
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Records found",
            Response = laboratoryJobOrders.Adapt<List<LaboratoryJobOrderResponse>>()
        };
    }
}