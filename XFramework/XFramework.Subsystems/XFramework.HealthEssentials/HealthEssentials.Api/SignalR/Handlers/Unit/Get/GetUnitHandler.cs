﻿using HealthEssentials.Core.DataAccess.Query.Entity.Unit;
using HealthEssentials.Domain.Generics.Contracts.Requests.Unit.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Unit;

namespace HealthEssentials.Api.SignalR.Handlers.Unit.Get;

public class GetUnitHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetUnitRequest, GetUnitQuery, UnitResponse>(connection, mediator);
    }
}