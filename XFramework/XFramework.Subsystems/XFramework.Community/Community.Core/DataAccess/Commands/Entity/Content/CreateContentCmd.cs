using Community.Domain.Generic.Contracts.Requests.Create;

namespace Community.Core.DataAccess.Commands.Entity.Content;

public class CreateContentCmd : CreateContentRequest, IRequest<CmdResponse>
{
    
}