using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class UpdateLaboratoryLocationDocumentHandler : CommandBaseHandler, IRequestHandler<UpdateLaboratoryLocationDocumentCmd, CmdResponse<UpdateLaboratoryLocationDocumentCmd>>
{
    public UpdateLaboratoryLocationDocumentHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateLaboratoryLocationDocumentCmd>> Handle(UpdateLaboratoryLocationDocumentCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}