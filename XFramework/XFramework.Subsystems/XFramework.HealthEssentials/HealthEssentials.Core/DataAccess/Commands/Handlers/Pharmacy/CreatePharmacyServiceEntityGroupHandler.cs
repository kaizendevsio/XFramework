using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class CreatePharmacyServiceEntityGroupHandler : CommandBaseHandler, IRequestHandler<CreatePharmacyServiceEntityGroupCmd, CmdResponse<CreatePharmacyServiceEntityGroupCmd>>
{
    public CreatePharmacyServiceEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreatePharmacyServiceEntityGroupCmd>> Handle(CreatePharmacyServiceEntityGroupCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}