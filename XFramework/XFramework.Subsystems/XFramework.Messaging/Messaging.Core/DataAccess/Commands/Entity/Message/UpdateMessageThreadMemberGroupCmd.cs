using Messaging.Domain.Generic.Contracts.Requests.Update;

namespace Messaging.Core.DataAccess.Commands.Entity.Message;

public class UpdateMessageThreadMemberGroupCmd : UpdateMessageThreadMemberGroupRequest, IRequest<CmdResponse<UpdateMessageThreadMemberGroupCmd>>
{
    
}