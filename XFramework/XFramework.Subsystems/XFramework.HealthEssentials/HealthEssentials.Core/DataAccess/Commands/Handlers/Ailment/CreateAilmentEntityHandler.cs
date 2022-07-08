using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Ailment;

public class CreateAilmentEntityHandler : CommandBaseHandler, IRequestHandler<CreateAilmentEntityCmd, CmdResponse<CreateAilmentEntityCmd>>
{
    public CreateAilmentEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateAilmentEntityCmd>> Handle(CreateAilmentEntityCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}