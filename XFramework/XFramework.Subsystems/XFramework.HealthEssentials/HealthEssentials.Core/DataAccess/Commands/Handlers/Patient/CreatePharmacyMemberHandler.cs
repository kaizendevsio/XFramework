using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class CreatePharmacyMemberHandler : CommandBaseHandler, IRequestHandler<CreatePharmacyMemberCmd, CmdResponse<CreatePharmacyMemberCmd>>
{
    public CreatePharmacyMemberHandler()
    {
        
    }
    public async Task<CmdResponse<CreatePharmacyMemberCmd>> Handle(CreatePharmacyMemberCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}