using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class UpdatePharmacyJobOrderConsultationJobOrderHandler : CommandBaseHandler, IRequestHandler<UpdatePharmacyJobOrderConsultationJobOrderCmd, CmdResponse<UpdatePharmacyJobOrderConsultationJobOrderCmd>>
{
    public UpdatePharmacyJobOrderConsultationJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdatePharmacyJobOrderConsultationJobOrderCmd>> Handle(UpdatePharmacyJobOrderConsultationJobOrderCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}