using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;
using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Create;

namespace HealthEssentials.Api.SignalR.Handlers.Logistic.Create;

public class CreateLogisticRiderHandleHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreateLogisticRiderHandleRequest, CreateLogisticRiderHandleCmd>(connection, mediator);
    }
}