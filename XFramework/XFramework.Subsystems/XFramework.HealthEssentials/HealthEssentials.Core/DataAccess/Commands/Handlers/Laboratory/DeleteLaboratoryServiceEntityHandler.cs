using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryServiceEntityHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryServiceEntityCmd, CmdResponse<DeleteLaboratoryServiceEntityCmd>>
{
    public DeleteLaboratoryServiceEntityHandler()
    {
        
    }
    public async Task<CmdResponse<DeleteLaboratoryServiceEntityCmd>> Handle(DeleteLaboratoryServiceEntityCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}