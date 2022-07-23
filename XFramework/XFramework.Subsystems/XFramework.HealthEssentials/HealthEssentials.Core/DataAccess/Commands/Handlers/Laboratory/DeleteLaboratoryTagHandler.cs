using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryTagHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryTagCmd, CmdResponse<DeleteLaboratoryTagCmd>>
{
    public DeleteLaboratoryTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteLaboratoryTagCmd>> Handle(DeleteLaboratoryTagCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}