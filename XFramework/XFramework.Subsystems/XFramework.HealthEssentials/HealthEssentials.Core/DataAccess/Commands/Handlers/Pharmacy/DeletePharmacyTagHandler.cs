using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class DeletePharmacyTagHandler : CommandBaseHandler, IRequestHandler<DeletePharmacyTagCmd, CmdResponse<DeletePharmacyTagCmd>>
{
    public DeletePharmacyTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePharmacyTagCmd>> Handle(DeletePharmacyTagCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}