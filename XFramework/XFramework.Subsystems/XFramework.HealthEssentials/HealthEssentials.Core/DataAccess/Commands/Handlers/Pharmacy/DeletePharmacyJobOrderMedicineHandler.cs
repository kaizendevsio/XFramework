using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class DeletePharmacyJobOrderMedicineHandler : CommandBaseHandler, IRequestHandler<DeletePharmacyJobOrderMedicineCmd, CmdResponse<DeletePharmacyJobOrderMedicineCmd>>
{
    public DeletePharmacyJobOrderMedicineHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePharmacyJobOrderMedicineCmd>> Handle(DeletePharmacyJobOrderMedicineCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}