﻿using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Create;

namespace HealthEssentials.Api.SignalR.Handlers.Patient.Create;

public class CreatePatientLaboratoryHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreatePatientLaboratoryRequest, CreatePatientLaboratoryCmd>(connection, mediator);
    }
}