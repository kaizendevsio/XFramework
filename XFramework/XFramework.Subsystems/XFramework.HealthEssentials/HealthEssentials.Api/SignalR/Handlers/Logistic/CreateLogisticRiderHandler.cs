﻿using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;
using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic;

namespace HealthEssentials.Api.SignalR.Handlers.Logistic;

public class CreateLogisticRiderHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreateLogisticRiderRequest, CreateLogisticRiderCmd>(connection, mediator);
    }
}