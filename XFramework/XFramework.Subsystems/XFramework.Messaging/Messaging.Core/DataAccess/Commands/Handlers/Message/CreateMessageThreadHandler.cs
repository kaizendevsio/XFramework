using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class CreateMessageThreadHandler : CommandBaseHandler, IRequestHandler<CreateMessageThreadCmd, CmdResponse<CreateMessageThreadCmd>>
{
    public CreateMessageThreadHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<CreateMessageThreadCmd>> Handle(CreateMessageThreadCmd request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.MessageThreadEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.EntityGuid}", CancellationToken.None);
        if (entity is null)
        {
            return new ()
            {
                Message = $"Message thread entity with guid {request.EntityGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var thread = request.Adapt<MessageThread>();
        thread.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        thread.Entity = entity;
        
        await _dataLayer.MessageThreads.AddAsync(thread, CancellationToken.None);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(thread.Guid);
        return new()
        {
            Message = $"Message thread with guid {thread.Guid} created",
            HttpStatusCode = HttpStatusCode.Accepted,
        };
    }
}