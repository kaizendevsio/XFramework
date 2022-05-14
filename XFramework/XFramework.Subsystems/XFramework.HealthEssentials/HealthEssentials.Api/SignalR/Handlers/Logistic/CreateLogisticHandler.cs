using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;
using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic;

namespace HealthEssentials.Api.SignalR.Handlers.Logistic;

public class CreateLogisticHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreateLogisticRequest, CreateLogisticCmd>(connection, mediator);
    }
}