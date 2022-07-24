using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class DeletePharmacyServiceTagHandler : CommandBaseHandler, IRequestHandler<DeletePharmacyServiceTagCmd, CmdResponse<DeletePharmacyServiceTagCmd>>
{
    public DeletePharmacyServiceTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePharmacyServiceTagCmd>> Handle(DeletePharmacyServiceTagCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}