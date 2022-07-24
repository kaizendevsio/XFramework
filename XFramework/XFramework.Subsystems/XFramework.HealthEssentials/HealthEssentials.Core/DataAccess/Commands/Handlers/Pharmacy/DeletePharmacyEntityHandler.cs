using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class DeletePharmacyEntityHandler : CommandBaseHandler, IRequestHandler<DeletePharmacyEntityCmd, CmdResponse<DeletePharmacyEntityCmd>>
{
    public DeletePharmacyEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePharmacyEntityCmd>> Handle(DeletePharmacyEntityCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}