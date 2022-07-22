using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;
using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Delete;

namespace HealthEssentials.Api.SignalR.Handlers.Consultation.Delete;

public class DeleteConsultationTagHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<DeleteConsultationTagRequest, DeleteConsultationTagCmd>(connection, mediator);
    }
}