using Messaging.Domain.Generic.Contracts.Requests.Delete;

namespace Messaging.Core.DataAccess.Commands.Entity.Message;

public class DeleteMessageThreadCmd : DeleteMessageThreadRequest, IRequest<CmdResponse<DeleteMessageThreadCmd>>
{
    
}