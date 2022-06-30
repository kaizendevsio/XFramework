using Community.Domain.Generic.Contracts.Requests.Create;

namespace Community.Core.DataAccess.Commands.Entity.Connection;

public class CreateConnectionCmd : CreateConnectionRequest, IRequest<CmdResponse>
{
    
}