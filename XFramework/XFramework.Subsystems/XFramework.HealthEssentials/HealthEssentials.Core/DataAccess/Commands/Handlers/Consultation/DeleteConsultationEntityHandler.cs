using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class DeleteConsultationEntityHandler : CommandBaseHandler, IRequestHandler<DeleteConsultationEntityCmd, CmdResponse<DeleteConsultationEntityCmd>>
{
    public DeleteConsultationEntityHandler()
    {
        
    }
    public async Task<CmdResponse<DeleteConsultationEntityCmd>> Handle(DeleteConsultationEntityCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}