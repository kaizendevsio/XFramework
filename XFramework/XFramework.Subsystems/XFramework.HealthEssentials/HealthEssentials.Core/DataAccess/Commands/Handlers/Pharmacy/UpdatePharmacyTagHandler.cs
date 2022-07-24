using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class UpdatePharmacyTagHandler : CommandBaseHandler, IRequestHandler<UpdatePharmacyTagCmd, CmdResponse<UpdatePharmacyTagCmd>>
{
    public UpdatePharmacyTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdatePharmacyTagCmd>> Handle(UpdatePharmacyTagCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}