using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class UpdatePharmacyStockHandler : CommandBaseHandler, IRequestHandler<UpdatePharmacyStockCmd, CmdResponse<UpdatePharmacyStockCmd>>
{
    public UpdatePharmacyStockHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdatePharmacyStockCmd>> Handle(UpdatePharmacyStockCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}