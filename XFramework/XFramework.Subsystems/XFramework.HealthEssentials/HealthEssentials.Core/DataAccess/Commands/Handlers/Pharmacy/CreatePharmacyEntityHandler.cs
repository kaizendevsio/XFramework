using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class CreatePharmacyEntityHandler : CommandBaseHandler, IRequestHandler<CreatePharmacyEntityCmd, CmdResponse<CreatePharmacyEntityCmd>>
{
    public CreatePharmacyEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreatePharmacyEntityCmd>> Handle(CreatePharmacyEntityCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}