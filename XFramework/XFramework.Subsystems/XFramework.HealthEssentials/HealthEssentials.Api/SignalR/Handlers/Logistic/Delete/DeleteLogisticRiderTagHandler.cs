using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Delete;

namespace HealthEssentials.Api.SignalR.Handlers.Logistic.Delete;

public class DeleteLogisticRiderTagHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeleteLogisticRiderTagRequest, DeleteLogisticRiderTagCmd>(connection, mediator);
    }
}