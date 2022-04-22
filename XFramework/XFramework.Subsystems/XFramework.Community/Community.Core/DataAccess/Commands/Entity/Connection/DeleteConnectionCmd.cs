using Community.Domain.Generic.Contracts.Requests.Create;
using Community.Domain.Generic.Contracts.Requests.Delete;

namespace Community.Core.DataAccess.Commands.Entity.Connection;

public class DeleteConnectionCmd : DeleteConnectionRequest, IRequest<CmdResponse>
{
    
}