using Messaging.Core.DataAccess.Commands.Entity.Message;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class ConfirmMessageSentHandler : CommandBaseHandler, IRequestHandler<ConfirmMessageSentCmd, CmdResponse>
{
    public ConfirmMessageSentHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse> Handle(ConfirmMessageSentCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}