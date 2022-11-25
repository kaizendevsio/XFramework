using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class UpdateMessageReactionHandler : CommandBaseHandler, IRequestHandler<UpdateMessageReactionCmd, CmdResponse<UpdateMessageReactionCmd>>
{
    public UpdateMessageReactionHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateMessageReactionCmd>> Handle(UpdateMessageReactionCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}