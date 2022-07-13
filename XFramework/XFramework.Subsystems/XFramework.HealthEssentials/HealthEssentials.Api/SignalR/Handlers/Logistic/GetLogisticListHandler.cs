﻿using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;
using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Logistic;

namespace HealthEssentials.Api.SignalR.Handlers.Logistic;

public class GetLogisticListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetLogisticListRequest, GetLogisticListQuery, List<LogisticResponse>>(connection, mediator);
    }
}