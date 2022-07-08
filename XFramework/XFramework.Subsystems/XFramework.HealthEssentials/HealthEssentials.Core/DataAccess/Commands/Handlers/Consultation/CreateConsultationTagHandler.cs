using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class CreateConsultationTagHandler : CommandBaseHandler, IRequestHandler<CreateConsultationTagCmd, CmdResponse<CreateConsultationTagCmd>>
{
    public CreateConsultationTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateConsultationTagCmd>> Handle(CreateConsultationTagCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}