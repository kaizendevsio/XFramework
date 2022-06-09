using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Doctor;

public class DeleteDoctorHandler : CommandBaseHandler, IRequestHandler<DeleteDoctorCmd, CmdResponse<DeleteDoctorCmd>>
{
    public DeleteDoctorHandler()
    {
        
    }
    public async Task<CmdResponse<DeleteDoctorCmd>> Handle(DeleteDoctorCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}