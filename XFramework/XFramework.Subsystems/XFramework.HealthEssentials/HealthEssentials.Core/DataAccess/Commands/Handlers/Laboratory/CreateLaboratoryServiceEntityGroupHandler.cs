using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;
using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class CreateLaboratoryServiceEntityGroupHandler : CommandBaseHandler, IRequestHandler<CreateLaboratoryServiceEntityGroupCmd, CmdResponse<CreateLaboratoryServiceEntityGroupCmd>>
{
    public CreateLaboratoryServiceEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLaboratoryServiceEntityGroupCmd>> Handle(CreateLaboratoryServiceEntityGroupCmd request, CancellationToken cancellationToken)
    {
        var entity = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.LaboratoryServiceEntityGroup>();
        entity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        
        _dataLayer.HealthEssentialsContext.LaboratoryServiceEntityGroups.Add(entity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Laboratory service type group with Guid {entity.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(entity.Guid)
            }
        };
    }
}