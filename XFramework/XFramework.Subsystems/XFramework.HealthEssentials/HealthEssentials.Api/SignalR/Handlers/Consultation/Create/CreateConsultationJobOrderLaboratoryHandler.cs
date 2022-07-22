using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;
using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Create;

namespace HealthEssentials.Api.SignalR.Handlers.Consultation.Create;

public class CreateConsultationJobOrderLaboratoryHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreateConsultationJobOrderLaboratoryRequest, CreateConsultationJobOrderLaboratoryCmd>(connection, mediator);
    }
}