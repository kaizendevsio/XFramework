﻿using HealthEssentials.Core.DataAccess.Query.Entity.Vendor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Vendor.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Vendor;

namespace HealthEssentials.Api.SignalR.Handlers.Vendor.Get;

public class GetVendorListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetVendorListRequest, GetVendorListQuery, List<VendorResponse>>(connection, mediator);
    }
}