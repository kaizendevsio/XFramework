using HealthEssentials.Core.DataAccess.Commands.Entity.Tag;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Tag;

public class CreateTagEntityGroupHandler : CommandBaseHandler, IRequestHandler<CreateTagEntityGroupCmd, CmdResponse<CreateTagEntityGroupCmd>>
{
    public CreateTagEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateTagEntityGroupCmd>> Handle(CreateTagEntityGroupCmd request, CancellationToken cancellationToken)
    {
        var group = request.Adapt<TagEntityGroup>();
        group.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";

        await _dataLayer.HealthEssentialsContext.TagEntityGroups.AddAsync(group, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(group.Guid);
        return new()
        {
            Message = $"Tag Entity Group {group.Name} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}