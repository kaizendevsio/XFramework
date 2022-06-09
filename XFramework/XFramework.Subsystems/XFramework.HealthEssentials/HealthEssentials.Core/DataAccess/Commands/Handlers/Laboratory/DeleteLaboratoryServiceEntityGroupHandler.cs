using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryServiceEntityGroupHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryServiceEntityGroupCmd, CmdResponse<DeleteLaboratoryServiceEntityGroupCmd>>
{
    public DeleteLaboratoryServiceEntityGroupHandler()
    {
        
    }
    public async Task<CmdResponse<DeleteLaboratoryServiceEntityGroupCmd>> Handle(DeleteLaboratoryServiceEntityGroupCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}