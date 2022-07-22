using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;
using HealthEssentials.Core.DataAccess.Query.Entity.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor;
using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Create;

namespace HealthEssentials.Api.SignalR.Handlers.Doctor.Create;

public class CreateDoctorHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestCmd<CreateDoctorRequest, CreateDoctorCmd>(connection, mediator);
    }
}