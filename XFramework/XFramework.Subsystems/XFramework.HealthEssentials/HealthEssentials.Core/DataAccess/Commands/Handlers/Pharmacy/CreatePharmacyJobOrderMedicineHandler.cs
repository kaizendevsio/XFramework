using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class CreatePharmacyJobOrderMedicineHandler : CommandBaseHandler, IRequestHandler<CreatePharmacyJobOrderMedicineCmd, CmdResponse<CreatePharmacyJobOrderMedicineCmd>>
{
    public CreatePharmacyJobOrderMedicineHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreatePharmacyJobOrderMedicineCmd>> Handle(CreatePharmacyJobOrderMedicineCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}