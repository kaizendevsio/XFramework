using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class DeletePharmacyServiceEntityGroupHandler : CommandBaseHandler, IRequestHandler<DeletePharmacyServiceEntityGroupCmd, CmdResponse<DeletePharmacyServiceEntityGroupCmd>>
{
    public DeletePharmacyServiceEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePharmacyServiceEntityGroupCmd>> Handle(DeletePharmacyServiceEntityGroupCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}