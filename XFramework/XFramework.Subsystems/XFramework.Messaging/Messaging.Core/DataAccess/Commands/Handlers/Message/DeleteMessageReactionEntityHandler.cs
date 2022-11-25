using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class DeleteMessageReactionEntityHandler : CommandBaseHandler, IRequestHandler<DeleteMessageReactionEntityCmd, CmdResponse<DeleteMessageReactionEntityCmd>>
{
    public DeleteMessageReactionEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteMessageReactionEntityCmd>> Handle(DeleteMessageReactionEntityCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}