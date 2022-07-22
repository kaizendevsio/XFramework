using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Update;

namespace HealthEssentials.Api.SignalR.Handlers.Logistic.Update;

public class UpdateLogisticRiderTagHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdateLogisticRiderTagRequest, UpdateLogisticRiderTagCmd>(connection, mediator);
    }
}