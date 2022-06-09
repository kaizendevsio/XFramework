using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class DeleteConsultationHandler : CommandBaseHandler, IRequestHandler<DeleteConsultationCmd, CmdResponse<DeleteConsultationCmd>>
{
    public DeleteConsultationHandler()
    {
        
    }
    public async Task<CmdResponse<DeleteConsultationCmd>> Handle(DeleteConsultationCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}