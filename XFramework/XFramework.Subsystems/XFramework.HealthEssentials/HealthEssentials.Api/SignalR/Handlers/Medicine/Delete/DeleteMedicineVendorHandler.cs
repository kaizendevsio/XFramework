﻿using HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;
using HealthEssentials.Domain.Generics.Contracts.Requests.Medicine.Delete;

namespace HealthEssentials.Api.SignalR.Handlers.Medicine.Delete;

public class DeleteMedicineVendorHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeleteMedicineVendorRequest, DeleteMedicineVendorCmd>(connection, mediator);
    }
}