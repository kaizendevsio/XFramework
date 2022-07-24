using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class DeletePharmacyJobOrderConsultationJobOrderHandler : CommandBaseHandler, IRequestHandler<DeletePharmacyJobOrderConsultationJobOrderCmd, CmdResponse<DeletePharmacyJobOrderConsultationJobOrderCmd>>
{
    public DeletePharmacyJobOrderConsultationJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePharmacyJobOrderConsultationJobOrderCmd>> Handle(DeletePharmacyJobOrderConsultationJobOrderCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}