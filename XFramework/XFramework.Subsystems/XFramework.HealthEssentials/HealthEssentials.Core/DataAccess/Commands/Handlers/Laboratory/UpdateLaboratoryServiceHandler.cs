using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class UpdateLaboratoryServiceHandler : CommandBaseHandler, IRequestHandler<UpdateLaboratoryServiceCmd, CmdResponse<UpdateLaboratoryServiceCmd>>
{
    public UpdateLaboratoryServiceHandler()
    {
        
    }
    public async Task<CmdResponse<UpdateLaboratoryServiceCmd>> Handle(UpdateLaboratoryServiceCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}