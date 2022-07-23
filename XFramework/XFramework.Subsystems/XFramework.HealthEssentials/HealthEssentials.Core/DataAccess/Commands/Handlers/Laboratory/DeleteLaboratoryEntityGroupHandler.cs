using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryEntityGroupHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryEntityGroupCmd, CmdResponse<DeleteLaboratoryEntityGroupCmd>>
{
    public DeleteLaboratoryEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteLaboratoryEntityGroupCmd>> Handle(DeleteLaboratoryEntityGroupCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}