using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryCmd, CmdResponse<DeleteLaboratoryCmd>>
{
    public DeleteLaboratoryHandler()
    {
        
    }
    public async Task<CmdResponse<DeleteLaboratoryCmd>> Handle(DeleteLaboratoryCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}