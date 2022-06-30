using Community.Domain.Generic.Contracts.Requests.Create;

namespace Community.Core.DataAccess.Commands.Entity.Reaction;

public class CreateReactionCmd : CreateReactionRequest, IRequest<CmdResponse>
{
    
}