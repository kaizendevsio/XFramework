using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class DeletePatientHandler : CommandBaseHandler, IRequestHandler<DeletePatientCmd, CmdResponse<DeletePatientCmd>>
{
    public DeletePatientHandler()
    {
        
    }
    public async Task<CmdResponse<DeletePatientCmd>> Handle(DeletePatientCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}