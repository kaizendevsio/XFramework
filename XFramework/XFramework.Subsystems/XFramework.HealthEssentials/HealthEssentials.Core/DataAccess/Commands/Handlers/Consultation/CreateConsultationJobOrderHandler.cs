using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class CreateConsultationJobOrderHandler : CommandBaseHandler, IRequestHandler<CreateConsultationJobOrderCmd, CmdResponse<CreateConsultationJobOrderCmd>>
{
    public CreateConsultationJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateConsultationJobOrderCmd>> Handle(CreateConsultationJobOrderCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}