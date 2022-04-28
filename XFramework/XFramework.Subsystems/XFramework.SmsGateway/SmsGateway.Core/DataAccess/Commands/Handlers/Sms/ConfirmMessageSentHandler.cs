using SmsGateway.Core.DataAccess.Commands.Entity.Sms;

namespace SmsGateway.Core.DataAccess.Commands.Handlers.Sms;

public class ConfirmMessageSentHandler : CommandBaseHandler, IRequestHandler<ConfirmSmsMessageSentCmd, CmdResponse>
{

    public ConfirmMessageSentHandler(ICachingService cachingService)
    {
        _cachingService = cachingService;
    }
    
    public async Task<CmdResponse> Handle(ConfirmSmsMessageSentCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}