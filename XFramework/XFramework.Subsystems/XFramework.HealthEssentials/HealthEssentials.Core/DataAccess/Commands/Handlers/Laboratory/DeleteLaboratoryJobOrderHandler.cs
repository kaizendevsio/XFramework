using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryJobOrderHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryJobOrderCmd, CmdResponse<DeleteLaboratoryJobOrderCmd>>
{
    public DeleteLaboratoryJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteLaboratoryJobOrderCmd>> Handle(DeleteLaboratoryJobOrderCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}