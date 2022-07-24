using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class UpdatePharmacyServiceHandler : CommandBaseHandler, IRequestHandler<UpdatePharmacyServiceCmd, CmdResponse<UpdatePharmacyServiceCmd>>
{
    public UpdatePharmacyServiceHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdatePharmacyServiceCmd>> Handle(UpdatePharmacyServiceCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}