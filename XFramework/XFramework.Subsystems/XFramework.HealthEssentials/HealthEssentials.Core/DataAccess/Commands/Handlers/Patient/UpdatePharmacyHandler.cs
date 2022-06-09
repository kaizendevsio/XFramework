using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class UpdatePharmacyHandler : CommandBaseHandler, IRequestHandler<UpdatePharmacyCmd, CmdResponse<UpdatePharmacyCmd>>
{
    public UpdatePharmacyHandler()
    {
        
    }
    public async Task<CmdResponse<UpdatePharmacyCmd>> Handle(UpdatePharmacyCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}