using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class UpdatePharmacyServiceTagHandler : CommandBaseHandler, IRequestHandler<UpdatePharmacyServiceTagCmd, CmdResponse<UpdatePharmacyServiceTagCmd>>
{
    public UpdatePharmacyServiceTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdatePharmacyServiceTagCmd>> Handle(UpdatePharmacyServiceTagCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}