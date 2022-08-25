using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class DeleteMessageThreadMemberGroupHandler : CommandBaseHandler, IRequestHandler<DeleteMessageThreadMemberGroupCmd, CmdResponse<DeleteMessageThreadMemberGroupCmd>>
{
    public DeleteMessageThreadMemberGroupHandler()
    {
        
    }
    public async Task<CmdResponse<DeleteMessageThreadMemberGroupCmd>> Handle(DeleteMessageThreadMemberGroupCmd request, CancellationToken cancellationToken)
    {
        var existingGroup = await _dataLayer.MessageThreadMemberGroups.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingGroup is null)
        {
            return new ()
            {
                Message = $"Group with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingGroup.IsDeleted = true;
        existingGroup.IsEnabled = false;
        
        _dataLayer.MessageThreadMemberGroups.Update(existingGroup);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Group with Guid {request.Guid} deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}