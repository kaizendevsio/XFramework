﻿using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyServiceListHandler : QueryBaseHandler, IRequestHandler<GetPharmacyServiceListQuery, QueryResponse<List<PharmacyServiceResponse>>>
{
    public GetPharmacyServiceListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PharmacyServiceResponse>>> Handle(GetPharmacyServiceListQuery request, CancellationToken cancellationToken)
    {
        var service = await _dataLayer.HealthEssentialsContext.PharmacyServices
            .Include(x => x.Entity)
            .ThenInclude(x => x.Group)
            .Include(x => x.PharmacyLocation)
            .ThenInclude(x => x.Pharmacy)
            .Include(x => x.Unit)
            .Where(x => EF.Functions.ILike(x.Name, $"{request.SearchField}"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!service.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No data found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Data found",
            IsSuccess = true,
            Response = service.Adapt<List<PharmacyServiceResponse>>()
        };
    }
}