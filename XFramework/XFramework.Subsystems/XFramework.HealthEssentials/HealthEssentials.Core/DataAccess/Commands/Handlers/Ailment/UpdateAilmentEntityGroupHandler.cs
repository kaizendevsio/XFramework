using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Ailment;

public class UpdateAilmentEntityGroupHandler : CommandBaseHandler, IRequestHandler<UpdateAilmentEntityGroupCmd, CmdResponse<UpdateAilmentEntityGroupCmd>>
{
    public UpdateAilmentEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateAilmentEntityGroupCmd>> Handle(UpdateAilmentEntityGroupCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}