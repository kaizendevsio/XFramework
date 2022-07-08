using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Ailment;

public class DeleteAilmentEntityGroupHandler : CommandBaseHandler, IRequestHandler<DeleteAilmentEntityGroupCmd, CmdResponse<DeleteAilmentEntityGroupCmd>>
{
    public DeleteAilmentEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteAilmentEntityGroupCmd>> Handle(DeleteAilmentEntityGroupCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}