using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class UpdatePharmacyMemberHandler : CommandBaseHandler, IRequestHandler<UpdatePharmacyMemberCmd, CmdResponse<UpdatePharmacyMemberCmd>>
{
    public UpdatePharmacyMemberHandler()
    {
        
    }
    public async Task<CmdResponse<UpdatePharmacyMemberCmd>> Handle(UpdatePharmacyMemberCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}