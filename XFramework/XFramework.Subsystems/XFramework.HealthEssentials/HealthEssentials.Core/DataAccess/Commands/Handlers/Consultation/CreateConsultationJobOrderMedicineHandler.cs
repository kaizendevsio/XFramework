using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class CreateConsultationJobOrderMedicineHandler : CommandBaseHandler, IRequestHandler<CreateConsultationJobOrderMedicineCmd, CmdResponse<CreateConsultationJobOrderMedicineCmd>>
{
    public CreateConsultationJobOrderMedicineHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateConsultationJobOrderMedicineCmd>> Handle(CreateConsultationJobOrderMedicineCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}