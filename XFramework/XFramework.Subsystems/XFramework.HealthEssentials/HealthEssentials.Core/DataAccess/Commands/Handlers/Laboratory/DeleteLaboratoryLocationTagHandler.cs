using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryLocationTagHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryLocationTagCmd, CmdResponse<DeleteLaboratoryLocationTagCmd>>
{
    public DeleteLaboratoryLocationTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteLaboratoryLocationTagCmd>> Handle(DeleteLaboratoryLocationTagCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}