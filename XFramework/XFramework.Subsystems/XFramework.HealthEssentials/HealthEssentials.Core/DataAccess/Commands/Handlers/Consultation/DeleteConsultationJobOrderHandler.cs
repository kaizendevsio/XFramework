using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class DeleteConsultationJobOrderHandler : CommandBaseHandler, IRequestHandler<DeleteConsultationJobOrderCmd, CmdResponse<DeleteConsultationJobOrderCmd>>
{
    public DeleteConsultationJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteConsultationJobOrderCmd>> Handle(DeleteConsultationJobOrderCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}