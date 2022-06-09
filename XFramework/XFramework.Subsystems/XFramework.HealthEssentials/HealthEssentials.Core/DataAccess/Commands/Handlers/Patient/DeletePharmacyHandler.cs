using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class DeletePharmacyHandler : CommandBaseHandler, IRequestHandler<DeletePharmacyCmd, CmdResponse<DeletePharmacyCmd>>
{
    public DeletePharmacyHandler()
    {
        
    }
    public async Task<CmdResponse<DeletePharmacyCmd>> Handle(DeletePharmacyCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}