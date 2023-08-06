﻿using System.Xml.Schema;
using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Hospital;

public class GetHospitalServiceHandler : QueryBaseHandler, IRequestHandler<GetHospitalServiceQuery, QueryResponse<HospitalServiceResponse>>
{
    public GetHospitalServiceHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<HospitalServiceResponse>> Handle(GetHospitalServiceQuery request, CancellationToken cancellationToken)
    {
        var service = await _dataLayer.HealthEssentialsContext.HospitalServices
            .Include(x => x.Type)
            .ThenInclude(x => x.Group)
            .Include(x => x.HospitalLocation)
            .ThenInclude(x => x.Hospital)
            .Include(x => x.Unit)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (service is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No record found",
                
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Record found",
            Response = service.Adapt<HospitalServiceResponse>()
        };
    }
}