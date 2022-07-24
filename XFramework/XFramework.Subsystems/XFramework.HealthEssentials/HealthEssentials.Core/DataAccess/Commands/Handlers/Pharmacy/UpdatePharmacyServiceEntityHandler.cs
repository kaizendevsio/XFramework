using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class UpdatePharmacyServiceEntityHandler : CommandBaseHandler, IRequestHandler<UpdatePharmacyServiceEntityCmd, CmdResponse<UpdatePharmacyServiceEntityCmd>>
{
    public UpdatePharmacyServiceEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdatePharmacyServiceEntityCmd>> Handle(UpdatePharmacyServiceEntityCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}