using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class CreatePharmacyTagHandler : CommandBaseHandler, IRequestHandler<CreatePharmacyTagCmd, CmdResponse<CreatePharmacyTagCmd>>
{
    public CreatePharmacyTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreatePharmacyTagCmd>> Handle(CreatePharmacyTagCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}