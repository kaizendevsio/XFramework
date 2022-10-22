using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class UpdateMessageReactionEntityHandler : CommandBaseHandler, IRequestHandler<UpdateMessageReactionEntityCmd, CmdResponse<UpdateMessageReactionEntityCmd>>
{
    public UpdateMessageReactionEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateMessageReactionEntityCmd>> Handle(UpdateMessageReactionEntityCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}