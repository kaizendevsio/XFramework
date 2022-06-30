using Community.Domain.Generic.Contracts.Requests.Delete;

namespace Community.Core.DataAccess.Commands.Entity.Identity;

public class DeleteIdentityCmd : DeleteIdentityRequest, IRequest<CmdResponse>
{
    
}