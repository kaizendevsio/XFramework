using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Ailment;

public class UpdateAilmentEntityHandler : CommandBaseHandler, IRequestHandler<UpdateAilmentEntityCmd, CmdResponse<UpdateAilmentEntityCmd>>
{
    public UpdateAilmentEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateAilmentEntityCmd>> Handle(UpdateAilmentEntityCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}