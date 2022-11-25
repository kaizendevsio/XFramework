﻿using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Get;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

public class GetLaboratoryServiceListQuery : GetLaboratoryServiceListRequest, IRequest<QueryResponse<List<LaboratoryServiceResponse>>>
{
    
}