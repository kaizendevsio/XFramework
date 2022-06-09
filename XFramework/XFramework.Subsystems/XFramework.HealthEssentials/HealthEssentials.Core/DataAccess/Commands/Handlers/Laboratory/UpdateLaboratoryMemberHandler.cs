using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class UpdateLaboratoryMemberHandler : CommandBaseHandler, IRequestHandler<UpdateLaboratoryMemberCmd, CmdResponse<UpdateLaboratoryMemberCmd>>
{
    public UpdateLaboratoryMemberHandler()
    {
        
    }
    public async Task<CmdResponse<UpdateLaboratoryMemberCmd>> Handle(UpdateLaboratoryMemberCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}