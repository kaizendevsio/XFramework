using HealthEssentials.Core.DataAccess.Commands.Entity.MetaData;
using HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Delete;

namespace HealthEssentials.Api.SignalR.Handlers.MetaData.Delete;

public class DeleteMetaDatumHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeleteMetaDatumRequest, DeleteMetaDatumCmd>(connection, mediator);
    }
}