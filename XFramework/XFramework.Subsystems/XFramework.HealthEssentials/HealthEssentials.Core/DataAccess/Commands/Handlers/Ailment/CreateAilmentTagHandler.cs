using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Ailment;

public class CreateAilmentTagHandler : CommandBaseHandler, IRequestHandler<CreateAilmentTagCmd, CmdResponse<CreateAilmentTagCmd>>
{
    public CreateAilmentTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateAilmentTagCmd>> Handle(CreateAilmentTagCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}