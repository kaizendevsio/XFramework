using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class UpdateLaboratoryServiceEntityGroupHandler : CommandBaseHandler, IRequestHandler<UpdateLaboratoryServiceEntityGroupCmd, CmdResponse<UpdateLaboratoryServiceEntityGroupCmd>>
{
    public UpdateLaboratoryServiceEntityGroupHandler()
    {
        
    }
    public async Task<CmdResponse<UpdateLaboratoryServiceEntityGroupCmd>> Handle(UpdateLaboratoryServiceEntityGroupCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}