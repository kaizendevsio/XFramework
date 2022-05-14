﻿using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;
using HealthEssentials.Core.DataAccess.Query.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient;

namespace HealthEssentials.Api.SignalR.Handlers.Patient;

public class CreatePatientIdentityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreatePatientIdentityRequest, CreatePatientIdentityCmd>(connection, mediator);
    }
}