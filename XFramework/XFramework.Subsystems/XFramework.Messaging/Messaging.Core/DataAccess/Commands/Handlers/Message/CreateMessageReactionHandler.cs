using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class CreateMessageReactionHandler : CommandBaseHandler, IRequestHandler<CreateMessageReactionCmd, CmdResponse<CreateMessageReactionCmd>>
{
    public CreateMessageReactionHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateMessageReactionCmd>> Handle(CreateMessageReactionCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}