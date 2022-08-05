using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryLocationDocumentHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryLocationDocumentCmd, CmdResponse<DeleteLaboratoryLocationDocumentCmd>>
{
    public DeleteLaboratoryLocationDocumentHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteLaboratoryLocationDocumentCmd>> Handle(DeleteLaboratoryLocationDocumentCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}