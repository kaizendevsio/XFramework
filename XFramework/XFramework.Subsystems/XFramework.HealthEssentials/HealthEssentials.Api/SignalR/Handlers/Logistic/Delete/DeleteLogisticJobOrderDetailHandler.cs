using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;
using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Delete;

namespace HealthEssentials.Api.SignalR.Handlers.Logistic.Delete;

public class DeleteLogisticJobOrderDetailHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeleteLogisticJobOrderDetailRequest, DeleteLogisticJobOrderDetailCmd>(connection, mediator);
    }
}