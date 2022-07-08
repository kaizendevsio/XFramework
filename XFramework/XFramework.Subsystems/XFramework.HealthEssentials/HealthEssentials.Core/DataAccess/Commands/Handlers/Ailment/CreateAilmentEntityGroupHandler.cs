using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Ailment;

public class CreateAilmentEntityGroupHandler : CommandBaseHandler, IRequestHandler<CreateAilmentEntityGroupCmd, CmdResponse<CreateAilmentEntityGroupCmd>>
{
    public CreateAilmentEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<CreateAilmentEntityGroupCmd>> Handle(CreateAilmentEntityGroupCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}