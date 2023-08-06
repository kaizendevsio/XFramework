using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;
using HealthEssentials.Domain.Generics.Contracts;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class CreateLaboratoryEntityGroupHandler : CommandBaseHandler, IRequestHandler<CreateLaboratoryEntityGroupCmd, CmdResponse<CreateLaboratoryEntityGroupCmd>>
{
    public CreateLaboratoryEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLaboratoryEntityGroupCmd>> Handle(CreateLaboratoryEntityGroupCmd request, CancellationToken cancellationToken)
    {
        var group = request.Adapt<LaboratoryTypeGroup>();
        group.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";

        await _dataLayer.HealthEssentialsContext.LaboratoryEntityGroups.AddAsync(group, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(group.Guid);
        return new()
        {
            Message = $"Laboratory Entity Group with Guid {group.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}