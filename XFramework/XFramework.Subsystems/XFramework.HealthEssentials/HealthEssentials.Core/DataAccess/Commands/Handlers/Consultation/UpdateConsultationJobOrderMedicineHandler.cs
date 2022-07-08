using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class UpdateConsultationJobOrderMedicineHandler : CommandBaseHandler, IRequestHandler<UpdateConsultationJobOrderMedicineCmd, CmdResponse<UpdateConsultationJobOrderMedicineCmd>>
{
    public UpdateConsultationJobOrderMedicineHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateConsultationJobOrderMedicineCmd>> Handle(UpdateConsultationJobOrderMedicineCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}