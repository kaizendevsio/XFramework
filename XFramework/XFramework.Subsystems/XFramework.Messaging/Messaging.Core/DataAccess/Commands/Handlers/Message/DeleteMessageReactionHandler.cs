using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class DeleteMessageReactionHandler : CommandBaseHandler, IRequestHandler<DeleteMessageReactionCmd, CmdResponse<DeleteMessageReactionCmd>>
{
    public DeleteMessageReactionHandler()
    {
        
    }
    public async Task<CmdResponse<DeleteMessageReactionCmd>> Handle(DeleteMessageReactionCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}