﻿using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Get;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

public class GetLogisticListQuery : GetLogisticListRequest, IRequest<QueryResponse<List<LogisticResponse>>>
{
    
}