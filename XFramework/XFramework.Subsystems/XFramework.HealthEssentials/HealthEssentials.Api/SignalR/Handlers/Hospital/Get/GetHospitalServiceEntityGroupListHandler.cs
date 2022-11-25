﻿using HealthEssentials.Core.DataAccess.Query.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

namespace HealthEssentials.Api.SignalR.Handlers.Hospital.Get;

public class GetHospitalServiceEntityGroupListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetHospitalServiceEntityGroupListRequest, GetHospitalServiceEntityGroupListQuery, List<HospitalServiceEntityGroupResponse>>(connection, mediator);
    }
}