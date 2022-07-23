using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryJobOrderResultHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryJobOrderResultCmd, CmdResponse<DeleteLaboratoryJobOrderResultCmd>>
{
    public DeleteLaboratoryJobOrderResultHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteLaboratoryJobOrderResultCmd>> Handle(DeleteLaboratoryJobOrderResultCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}