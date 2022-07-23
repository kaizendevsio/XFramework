using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class CreateLaboratoryLocationDocumentHandler : CommandBaseHandler, IRequestHandler<CreateLaboratoryLocationDocumentCmd, CmdResponse<CreateLaboratoryLocationDocumentCmd>>
{
    public CreateLaboratoryLocationDocumentHandler()
    {
        
    }
    public async Task<CmdResponse<CreateLaboratoryLocationDocumentCmd>> Handle(CreateLaboratoryLocationDocumentCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}