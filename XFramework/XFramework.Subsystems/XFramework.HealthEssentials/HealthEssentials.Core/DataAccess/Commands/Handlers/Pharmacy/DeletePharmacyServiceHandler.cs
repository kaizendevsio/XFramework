using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class DeletePharmacyServiceHandler : CommandBaseHandler, IRequestHandler<DeletePharmacyServiceCmd, CmdResponse<DeletePharmacyServiceCmd>>
{
    public DeletePharmacyServiceHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePharmacyServiceCmd>> Handle(DeletePharmacyServiceCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}