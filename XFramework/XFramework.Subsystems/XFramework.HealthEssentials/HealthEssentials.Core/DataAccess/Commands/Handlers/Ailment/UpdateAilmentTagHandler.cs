using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Ailment;

public class UpdateAilmentTagHandler : CommandBaseHandler, IRequestHandler<UpdateAilmentTagCmd, CmdResponse<UpdateAilmentTagCmd>>
{
    public UpdateAilmentTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateAilmentTagCmd>> Handle(UpdateAilmentTagCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}