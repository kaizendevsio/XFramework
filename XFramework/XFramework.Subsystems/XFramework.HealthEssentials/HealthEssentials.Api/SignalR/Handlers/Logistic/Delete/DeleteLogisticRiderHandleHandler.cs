using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;
using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Delete;

namespace HealthEssentials.Api.SignalR.Handlers.Logistic.Delete;

public class DeleteLogisticRiderHandleHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeleteLogisticRiderHandleRequest, DeleteLogisticRiderHandleCmd>(connection, mediator);
    }
}