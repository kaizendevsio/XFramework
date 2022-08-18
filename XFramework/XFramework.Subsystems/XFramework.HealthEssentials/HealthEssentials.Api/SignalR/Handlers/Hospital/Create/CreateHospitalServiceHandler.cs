using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Create;

namespace HealthEssentials.Api.SignalR.Handlers.Hospital.Create;

public class CreateHospitalServiceHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreateHospitalServiceRequest, CreateHospitalServiceCmd>(connection, mediator);
    }
}