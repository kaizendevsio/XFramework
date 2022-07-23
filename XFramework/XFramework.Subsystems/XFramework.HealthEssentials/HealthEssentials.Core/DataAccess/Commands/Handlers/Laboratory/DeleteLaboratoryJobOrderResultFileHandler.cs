using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryJobOrderResultFileHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryJobOrderResultFileCmd, CmdResponse<DeleteLaboratoryJobOrderResultFileCmd>>
{
    public DeleteLaboratoryJobOrderResultFileHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteLaboratoryJobOrderResultFileCmd>> Handle(DeleteLaboratoryJobOrderResultFileCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}