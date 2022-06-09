using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class DeletePharmacyMemberHandler : CommandBaseHandler, IRequestHandler<DeletePharmacyMemberCmd, CmdResponse<DeletePharmacyMemberCmd>>
{
    public DeletePharmacyMemberHandler()
    {
        
    }
    public async Task<CmdResponse<DeletePharmacyMemberCmd>> Handle(DeletePharmacyMemberCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}