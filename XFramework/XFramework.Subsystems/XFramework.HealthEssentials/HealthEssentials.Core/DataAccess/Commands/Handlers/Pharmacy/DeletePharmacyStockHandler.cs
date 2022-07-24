using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class DeletePharmacyStockHandler : CommandBaseHandler, IRequestHandler<DeletePharmacyStockCmd, CmdResponse<DeletePharmacyStockCmd>>
{
    public DeletePharmacyStockHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePharmacyStockCmd>> Handle(DeletePharmacyStockCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}