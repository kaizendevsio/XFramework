using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class CreatePharmacyServiceEntityHandler : CommandBaseHandler, IRequestHandler<CreatePharmacyServiceEntityCmd, CmdResponse<CreatePharmacyServiceEntityCmd>>
{
    public CreatePharmacyServiceEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreatePharmacyServiceEntityCmd>> Handle(CreatePharmacyServiceEntityCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}