using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;
using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Update;

namespace HealthEssentials.Api.SignalR.Handlers.Consultation.Update;

public class UpdateConsultationTagHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdateConsultationTagRequest, UpdateConsultationTagCmd>(connection, mediator);
    }
}