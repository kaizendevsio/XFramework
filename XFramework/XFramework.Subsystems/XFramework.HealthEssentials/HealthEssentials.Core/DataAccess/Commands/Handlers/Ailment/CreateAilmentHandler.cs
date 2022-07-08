using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Ailment;

public class CreateAilmentHandler : CommandBaseHandler, IRequestHandler<CreateAilmentCmd, CmdResponse<CreateAilmentCmd>>
{
    public CreateAilmentHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateAilmentCmd>> Handle(CreateAilmentCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}