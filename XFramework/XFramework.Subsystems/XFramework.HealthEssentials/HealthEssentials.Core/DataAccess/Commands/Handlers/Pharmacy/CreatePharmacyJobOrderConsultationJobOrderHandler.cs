using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class CreatePharmacyJobOrderConsultationJobOrderHandler : CommandBaseHandler, IRequestHandler<CreatePharmacyJobOrderConsultationJobOrderCmd, CmdResponse<CreatePharmacyJobOrderConsultationJobOrderCmd>>
{
    public CreatePharmacyJobOrderConsultationJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreatePharmacyJobOrderConsultationJobOrderCmd>> Handle(CreatePharmacyJobOrderConsultationJobOrderCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}