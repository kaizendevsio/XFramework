using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class DeletePharmacyJobOrderHandler : CommandBaseHandler, IRequestHandler<DeletePharmacyJobOrderCmd, CmdResponse<DeletePharmacyJobOrderCmd>>
{
    public DeletePharmacyJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePharmacyJobOrderCmd>> Handle(DeletePharmacyJobOrderCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}