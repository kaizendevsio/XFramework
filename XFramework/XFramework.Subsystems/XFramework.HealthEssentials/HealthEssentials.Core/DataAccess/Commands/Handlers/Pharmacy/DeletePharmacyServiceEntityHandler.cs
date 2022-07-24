using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class DeletePharmacyServiceEntityHandler : CommandBaseHandler, IRequestHandler<DeletePharmacyServiceEntityCmd, CmdResponse<DeletePharmacyServiceEntityCmd>>
{
    public DeletePharmacyServiceEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePharmacyServiceEntityCmd>> Handle(DeletePharmacyServiceEntityCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}