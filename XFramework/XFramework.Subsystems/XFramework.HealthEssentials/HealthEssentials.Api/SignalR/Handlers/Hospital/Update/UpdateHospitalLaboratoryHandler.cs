﻿using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Update;

namespace HealthEssentials.Api.SignalR.Handlers.Hospital.Update;

public class UpdateHospitalLaboratoryHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdateHospitalLaboratoryRequest, UpdateHospitalLaboratoryCmd>(connection, mediator);
    }
}