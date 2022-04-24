using SmsGateway.Core.DataAccess.Commands.Entity.Sms;

namespace SmsGateway.Core.DataAccess.Commands.Handlers.Sms;

public class CreateSmsMessageHandler : CommandBaseHandler, IRequestHandler<CreateSmsMessageCmd, CmdResponse<CreateSmsMessageCmd>>
{
    public CreateSmsMessageHandler(IDataLayer dataLayer, ICachingService cachingService)
    {
        _cachingService = cachingService;
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateSmsMessageCmd>> Handle(CreateSmsMessageCmd request, CancellationToken cancellationToken)
    {
        
        
        //_cachingService.PendingMessageList.
        
    }
}