using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class UpdatePharmacyEntityHandler : CommandBaseHandler, IRequestHandler<UpdatePharmacyEntityCmd, CmdResponse<UpdatePharmacyEntityCmd>>
{
    public UpdatePharmacyEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdatePharmacyEntityCmd>> Handle(UpdatePharmacyEntityCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}