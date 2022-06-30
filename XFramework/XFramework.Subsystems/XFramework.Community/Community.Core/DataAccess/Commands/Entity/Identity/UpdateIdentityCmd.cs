using Community.Domain.Generic.Contracts.Requests.Update;

namespace Community.Core.DataAccess.Commands.Entity.Identity;

public class UpdateIdentityCmd : UpdateIdentityRequest, IRequest<CmdResponse>
{
    
}