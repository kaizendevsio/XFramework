using Community.Domain.Generic.Contracts.Requests.Create;

namespace Community.Core.DataAccess.Commands.Entity.Identity;

public class CreateIdentityCmd : CreateIdentityRequest, IRequest<CmdResponse>
{
    
}