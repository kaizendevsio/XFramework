using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Ailment;

public class UpdateAilmentHandler : CommandBaseHandler, IRequestHandler<UpdateAilmentCmd, CmdResponse<UpdateAilmentCmd>>
{
    public UpdateAilmentHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateAilmentCmd>> Handle(UpdateAilmentCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}