using SmsGateway.Domain.Generic.Contracts.Requests.Update;

namespace SmsGateway.Core.DataAccess.Commands.Entity.Sms;

public class ConfirmSmsMessageSentCmd : ConfirmSmsMessageSentRequest, IRequest<CmdResponse>
{
    
}