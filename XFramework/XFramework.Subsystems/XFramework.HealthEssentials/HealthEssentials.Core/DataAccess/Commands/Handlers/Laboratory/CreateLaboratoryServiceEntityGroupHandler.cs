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
        var serviceEntityGroup = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.LaboratoryServiceEntityGroup>();
        serviceEntityGroup.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        
        await _dataLayer.HealthEssentialsContext.LaboratoryServiceEntityGroups.AddAsync(serviceEntityGroup, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        request.Guid = Guid.Parse(serviceEntityGroup.Guid);
        return new()
        {
            Message = $"Laboratory service entity group with Guid {serviceEntityGroup.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}