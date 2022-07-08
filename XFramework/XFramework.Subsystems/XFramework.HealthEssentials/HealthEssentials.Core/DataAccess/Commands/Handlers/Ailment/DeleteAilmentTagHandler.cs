using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Ailment;

public class DeleteAilmentTagHandler : CommandBaseHandler, IRequestHandler<DeleteAilmentTagCmd, CmdResponse<DeleteAilmentTagCmd>>
{
    public DeleteAilmentTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteAilmentTagCmd>> Handle(DeleteAilmentTagCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}