﻿using HealthEssentials.Core.DataAccess.Query.Entity.Patient;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

namespace HealthEssentials.Api.SignalR.Handlers.Patient.Get;

public class GetPatientAilmentHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetPatientAilmentRequest, GetPatientAilmentQuery, PatientAilmentResponse>(connection, mediator);
    }
}