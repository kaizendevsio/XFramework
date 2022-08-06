﻿using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyJobOrderListHandler : QueryBaseHandler, IRequestHandler<GetPharmacyJobOrderListQuery, QueryResponse<List<PharmacyJobOrderResponse>>>
{
    public GetPharmacyJobOrderListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PharmacyJobOrderResponse>>> Handle(GetPharmacyJobOrderListQuery request, CancellationToken cancellationToken)
    {
        var jobOrder = await _dataLayer.HealthEssentialsContext.PharmacyJobOrders
            .Include(i => i.PharmacyLocation)
            .Include(i => i.Schedule)
            .Include(x => x.Patient)
            .Where(i => EF.Functions.ILike(i.ReferenceNumber, $"%{request.SearchField}%"))
            .Where(i => i.PharmacyLocation.Guid == $"{request.PharmacyLocationGuid}")
            .OrderBy(i => i.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        if (!jobOrder.Any())
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
            IsSuccess = true,
            Response = jobOrder.Adapt<List<PharmacyJobOrderResponse>>()
        };
    }
}