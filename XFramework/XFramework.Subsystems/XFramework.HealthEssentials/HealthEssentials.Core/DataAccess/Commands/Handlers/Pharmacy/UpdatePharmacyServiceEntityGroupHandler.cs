using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class UpdatePharmacyServiceEntityGroupHandler : CommandBaseHandler, IRequestHandler<UpdatePharmacyServiceEntityGroupCmd, CmdResponse<UpdatePharmacyServiceEntityGroupCmd>>
{
    public UpdatePharmacyServiceEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdatePharmacyServiceEntityGroupCmd>> Handle(UpdatePharmacyServiceEntityGroupCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}