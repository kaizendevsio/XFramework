using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class CreateMessageReactionEntityHandler : CommandBaseHandler, IRequestHandler<CreateMessageReactionEntityCmd, CmdResponse<CreateMessageReactionEntityCmd>>
{
    public CreateMessageReactionEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateMessageReactionEntityCmd>> Handle(CreateMessageReactionEntityCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}