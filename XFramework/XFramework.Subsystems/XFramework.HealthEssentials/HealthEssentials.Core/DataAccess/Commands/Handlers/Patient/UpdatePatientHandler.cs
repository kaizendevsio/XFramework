using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class UpdatePatientHandler : CommandBaseHandler, IRequestHandler<UpdatePatientCmd, CmdResponse<UpdatePatientCmd>>
{
    public UpdatePatientHandler()
    {
        
    }
    public async Task<CmdResponse<UpdatePatientCmd>> Handle(UpdatePatientCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}