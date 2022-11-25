using Messaging.Domain.Generic.Contracts.Requests.Update;

namespace Messaging.Core.DataAccess.Commands.Entity.Message;

public class UpdateMessageTypeCmd : UpdateMessageTypeRequest, IRequest<CmdResponse<UpdateMessageTypeCmd>>
{
    
}