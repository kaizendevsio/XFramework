using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;
using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation;
using HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Update;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor;

namespace HealthEssentials.Api.SignalR.Handlers.Consultation.Update;

public class UpdateConsultationHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<UpdateConsultationRequest, UpdateConsultationCmd>(connection, mediator);
    }
}