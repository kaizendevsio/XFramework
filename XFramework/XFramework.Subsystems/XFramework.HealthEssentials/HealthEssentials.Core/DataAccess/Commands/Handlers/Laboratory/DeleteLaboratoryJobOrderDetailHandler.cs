using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryJobOrderDetailHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryJobOrderDetailCmd, CmdResponse<DeleteLaboratoryJobOrderDetailCmd>>
{
    public DeleteLaboratoryJobOrderDetailHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteLaboratoryJobOrderDetailCmd>> Handle(DeleteLaboratoryJobOrderDetailCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}