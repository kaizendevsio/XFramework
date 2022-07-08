using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class UpdateConsultationTagHandler : CommandBaseHandler, IRequestHandler<UpdateConsultationTagCmd, CmdResponse<UpdateConsultationTagCmd>>
{
    public UpdateConsultationTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateConsultationTagCmd>> Handle(UpdateConsultationTagCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}