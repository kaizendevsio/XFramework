using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class DeleteConsultationJobOrderLaboratoryHandler : CommandBaseHandler, IRequestHandler<DeleteConsultationJobOrderLaboratoryCmd, CmdResponse<DeleteConsultationJobOrderLaboratoryCmd>>
{
    public DeleteConsultationJobOrderLaboratoryHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteConsultationJobOrderLaboratoryCmd>> Handle(DeleteConsultationJobOrderLaboratoryCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}