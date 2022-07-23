using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryServiceTagHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryServiceTagCmd, CmdResponse<DeleteLaboratoryServiceTagCmd>>
{
    public DeleteLaboratoryServiceTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteLaboratoryServiceTagCmd>> Handle(DeleteLaboratoryServiceTagCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}