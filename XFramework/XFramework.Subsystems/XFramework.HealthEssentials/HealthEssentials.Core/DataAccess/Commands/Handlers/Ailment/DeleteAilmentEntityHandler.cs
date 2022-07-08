using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Ailment;

public class DeleteAilmentEntityHandler : CommandBaseHandler, IRequestHandler<DeleteAilmentEntityCmd, CmdResponse<DeleteAilmentEntityCmd>>
{
    public DeleteAilmentEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteAilmentEntityCmd>> Handle(DeleteAilmentEntityCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}