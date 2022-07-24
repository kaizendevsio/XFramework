using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class UpdatePharmacyJobOrderMedicineHandler : CommandBaseHandler, IRequestHandler<UpdatePharmacyJobOrderMedicineCmd, CmdResponse<UpdatePharmacyJobOrderMedicineCmd>>
{
    public UpdatePharmacyJobOrderMedicineHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdatePharmacyJobOrderMedicineCmd>> Handle(UpdatePharmacyJobOrderMedicineCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}