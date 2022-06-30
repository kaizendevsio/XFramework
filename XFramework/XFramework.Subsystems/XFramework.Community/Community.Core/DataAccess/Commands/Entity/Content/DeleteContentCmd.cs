using Community.Domain.Generic.Contracts.Requests.Delete;

namespace Community.Core.DataAccess.Commands.Entity.Content;

public class DeleteContentCmd : DeleteContentRequest, IRequest<CmdResponse>
{
    
}