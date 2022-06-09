using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryMemberHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryMemberCmd, CmdResponse<DeleteLaboratoryMemberCmd>>
{
    public DeleteLaboratoryMemberHandler()
    {
        
    }
    public async Task<CmdResponse<DeleteLaboratoryMemberCmd>> Handle(DeleteLaboratoryMemberCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}