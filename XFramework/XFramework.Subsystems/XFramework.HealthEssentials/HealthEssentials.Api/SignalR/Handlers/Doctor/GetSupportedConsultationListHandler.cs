﻿using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;
using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;

namespace HealthEssentials.Api.SignalR.Handlers.Doctor;

public class GetSupportedConsultationListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetSupportedConsultationListRequest, GetSupportedConsultationListQuery, List<DoctorConsultationResponse>>(connection, mediator);
    }
}