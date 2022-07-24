using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class CreatePharmacyJobOrderHandler : CommandBaseHandler, IRequestHandler<CreatePharmacyJobOrderCmd, CmdResponse<CreatePharmacyJobOrderCmd>>
{
    public CreatePharmacyJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreatePharmacyJobOrderCmd>> Handle(CreatePharmacyJobOrderCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}