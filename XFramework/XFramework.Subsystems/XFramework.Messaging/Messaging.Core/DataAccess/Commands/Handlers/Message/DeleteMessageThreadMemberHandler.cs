using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class DeleteMessageThreadMemberHandler : CommandBaseHandler, IRequestHandler<DeleteMessageThreadMemberCmd, CmdResponse<DeleteMessageThreadMemberCmd>>
{
    public DeleteMessageThreadMemberHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<DeleteMessageThreadMemberCmd>> Handle(DeleteMessageThreadMemberCmd request, CancellationToken cancellationToken)
    {
        var existingMember = await _dataLayer.MessageThreadMembers.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingMember is null)
        {
            return new ()
            {
                Message = $"Member with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingMember.IsDeleted = true;
        existingMember.IsEnabled = false;
        
        _dataLayer.MessageThreadMembers.Update(existingMember);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Member with Guid {request.Guid} deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}