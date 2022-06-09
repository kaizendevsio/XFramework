using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryServiceHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryServiceCmd, CmdResponse<DeleteLaboratoryServiceCmd>>
{
    public DeleteLaboratoryServiceHandler()
    {
        
    }
    public async Task<CmdResponse<DeleteLaboratoryServiceCmd>> Handle(DeleteLaboratoryServiceCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}