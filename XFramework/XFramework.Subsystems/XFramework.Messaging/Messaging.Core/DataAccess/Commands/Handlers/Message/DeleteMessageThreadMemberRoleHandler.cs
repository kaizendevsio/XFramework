using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class DeleteMessageThreadMemberRoleHandler : CommandBaseHandler, IRequestHandler<DeleteMessageThreadMemberRoleCmd, CmdResponse<DeleteMessageThreadMemberRoleCmd>>
{
    public DeleteMessageThreadMemberRoleHandler()
    {
        
    }
    public async Task<CmdResponse<DeleteMessageThreadMemberRoleCmd>> Handle(DeleteMessageThreadMemberRoleCmd request, CancellationToken cancellationToken)
    {
        var existingRole = await _dataLayer.MessageThreadMemberRoles.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingRole is null)
        {
            return new ()
            {
                Message = $"Role with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingRole.IsDeleted = true;
        existingRole.IsEnabled = false;
        
        _dataLayer.MessageThreadMemberRoles.Update(existingRole);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Role with Guid {request.Guid} deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}