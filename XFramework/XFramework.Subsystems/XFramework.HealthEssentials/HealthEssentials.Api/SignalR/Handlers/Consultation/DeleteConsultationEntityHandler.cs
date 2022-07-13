﻿using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;
using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation;
using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Delete;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor;

namespace HealthEssentials.Api.SignalR.Handlers.Consultation;

public class DeleteConsultationEntityHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeleteConsultationEntityRequest, DeleteConsultationEntityCmd>(connection, mediator);
    }
}