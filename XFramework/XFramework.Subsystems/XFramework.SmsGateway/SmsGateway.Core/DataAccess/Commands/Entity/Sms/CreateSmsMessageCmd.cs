using SmsGateway.Domain.Generic.Contracts.Requests.Create;

namespace SmsGateway.Core.DataAccess.Commands.Entity.Sms;

public class CreateSmsMessageCmd : CreateSmsMessageRequest, IRequest<CmdResponse>
{
    
}