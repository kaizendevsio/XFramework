using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;
using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Create;

namespace HealthEssentials.Api.SignalR.Handlers.Consultation.Create;

public class CreateConsultationJobOrderHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreateConsultationJobOrderRequest, CreateConsultationJobOrderCmd>(connection, mediator);
    }
}