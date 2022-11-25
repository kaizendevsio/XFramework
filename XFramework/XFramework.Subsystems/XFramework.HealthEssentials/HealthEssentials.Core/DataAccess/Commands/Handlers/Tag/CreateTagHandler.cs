using HealthEssentials.Core.DataAccess.Commands.Entity.Tag;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Tag;

public class CreateTagHandler : CommandBaseHandler, IRequestHandler<CreateTagCmd, CmdResponse<CreateTagCmd>>
{
    public CreateTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateTagCmd>> Handle(CreateTagCmd request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.HealthEssentialsContext.TagEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.EntityGuid}", CancellationToken.None);
        if (entity is null)
        {
            return new ()
            {
                Message = $"Tag with Guid {request.EntityGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var tag = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.Tag>();
        tag.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        tag.Entity = entity;
        
        await _dataLayer.HealthEssentialsContext.Tags.AddAsync(tag, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(tag.Guid);
        return new()
        {
            Message = $"Tag with Guid {tag.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };


    }
}