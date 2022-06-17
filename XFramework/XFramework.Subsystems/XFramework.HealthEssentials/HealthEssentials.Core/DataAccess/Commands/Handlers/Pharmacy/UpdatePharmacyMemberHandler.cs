using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;
using HealthEssentials.Domain.DataTransferObjects;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class UpdatePharmacyMemberHandler : CommandBaseHandler, IRequestHandler<UpdatePharmacyMemberCmd, CmdResponse<UpdatePharmacyMemberCmd>>
{
    public UpdatePharmacyMemberHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdatePharmacyMemberCmd>> Handle(UpdatePharmacyMemberCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
