using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class CreatePharmacyServiceTagHandler : CommandBaseHandler, IRequestHandler<CreatePharmacyServiceTagCmd, CmdResponse<CreatePharmacyServiceTagCmd>>
{
    public CreatePharmacyServiceTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreatePharmacyServiceTagCmd>> Handle(CreatePharmacyServiceTagCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}