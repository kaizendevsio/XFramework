using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class CreateConsultationJobOrderLaboratoryHandler : CommandBaseHandler, IRequestHandler<CreateConsultationJobOrderLaboratoryCmd, CmdResponse<CreateConsultationJobOrderLaboratoryCmd>>
{
    public CreateConsultationJobOrderLaboratoryHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateConsultationJobOrderLaboratoryCmd>> Handle(CreateConsultationJobOrderLaboratoryCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}