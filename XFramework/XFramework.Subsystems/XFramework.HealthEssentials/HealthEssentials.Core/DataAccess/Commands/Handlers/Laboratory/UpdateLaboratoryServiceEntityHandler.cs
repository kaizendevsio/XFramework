using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class UpdateLaboratoryServiceEntityHandler : CommandBaseHandler, IRequestHandler<UpdateLaboratoryServiceEntityCmd, CmdResponse<UpdateLaboratoryServiceEntityCmd>>
{
    public UpdateLaboratoryServiceEntityHandler()
    {
        
    }
    public async Task<CmdResponse<UpdateLaboratoryServiceEntityCmd>> Handle(UpdateLaboratoryServiceEntityCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}