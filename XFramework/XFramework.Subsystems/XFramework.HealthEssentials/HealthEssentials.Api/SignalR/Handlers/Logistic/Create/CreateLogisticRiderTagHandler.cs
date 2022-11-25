using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Create;

namespace HealthEssentials.Api.SignalR.Handlers.Logistic.Create;

public class CreateLogisticRiderTagHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreateLogisticRiderTagRequest, CreateLogisticRiderTagCmd>(connection, mediator);
    }
}