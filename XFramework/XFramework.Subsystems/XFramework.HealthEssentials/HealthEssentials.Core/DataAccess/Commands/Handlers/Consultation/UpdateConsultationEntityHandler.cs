using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class UpdateConsultationEntityHandler : CommandBaseHandler, IRequestHandler<UpdateConsultationEntityCmd, CmdResponse<UpdateConsultationEntityCmd>>
{
    public UpdateConsultationEntityHandler()
    {
        
    }
    public async Task<CmdResponse<UpdateConsultationEntityCmd>> Handle(UpdateConsultationEntityCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}