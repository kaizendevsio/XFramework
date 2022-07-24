using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class CreatePharmacyStockHandler : CommandBaseHandler, IRequestHandler<CreatePharmacyStockCmd, CmdResponse<CreatePharmacyStockCmd>>
{
    public CreatePharmacyStockHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreatePharmacyStockCmd>> Handle(CreatePharmacyStockCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}