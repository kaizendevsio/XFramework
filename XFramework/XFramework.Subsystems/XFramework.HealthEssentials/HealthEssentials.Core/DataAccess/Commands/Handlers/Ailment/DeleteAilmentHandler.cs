using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Ailment;

public class DeleteAilmentHandler : CommandBaseHandler, IRequestHandler<DeleteAilmentCmd, CmdResponse<DeleteAilmentCmd>>
{
    public DeleteAilmentHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteAilmentCmd>> Handle(DeleteAilmentCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}