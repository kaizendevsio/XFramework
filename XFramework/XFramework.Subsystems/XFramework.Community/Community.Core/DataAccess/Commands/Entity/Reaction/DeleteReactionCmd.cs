using Community.Domain.Generic.Contracts.Requests.Delete;

namespace Community.Core.DataAccess.Commands.Entity.Reaction;

public class DeleteReactionCmd : DeleteReactionRequest, IRequest<CmdResponse>
{
    
}