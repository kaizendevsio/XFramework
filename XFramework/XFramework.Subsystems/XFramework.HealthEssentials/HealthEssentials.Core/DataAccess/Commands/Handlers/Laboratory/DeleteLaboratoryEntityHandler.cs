using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryEntityHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryEntityCmd, CmdResponse<DeleteLaboratoryEntityCmd>>
{
    public DeleteLaboratoryEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteLaboratoryEntityCmd>> Handle(DeleteLaboratoryEntityCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}