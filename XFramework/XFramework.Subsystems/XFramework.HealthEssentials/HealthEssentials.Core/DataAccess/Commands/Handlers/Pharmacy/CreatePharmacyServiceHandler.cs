using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class CreatePharmacyServiceHandler : CommandBaseHandler, IRequestHandler<CreatePharmacyServiceCmd, CmdResponse<CreatePharmacyServiceCmd>>
{
    public CreatePharmacyServiceHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreatePharmacyServiceCmd>> Handle(CreatePharmacyServiceCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}