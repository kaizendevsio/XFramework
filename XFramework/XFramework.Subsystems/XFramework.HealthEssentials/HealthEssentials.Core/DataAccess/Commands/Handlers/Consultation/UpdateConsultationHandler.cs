using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class UpdateConsultationHandler : CommandBaseHandler, IRequestHandler<UpdateConsultationCmd, CmdResponse<UpdateConsultationCmd>>
{
    public UpdateConsultationHandler()
    {
        
    }
    public async Task<CmdResponse<UpdateConsultationCmd>> Handle(UpdateConsultationCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}