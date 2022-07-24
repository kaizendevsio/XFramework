using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class UpdatePharmacyJobOrderHandler : CommandBaseHandler, IRequestHandler<UpdatePharmacyJobOrderCmd, CmdResponse<UpdatePharmacyJobOrderCmd>>
{
    public UpdatePharmacyJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdatePharmacyJobOrderCmd>> Handle(UpdatePharmacyJobOrderCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}