using IdentityServer.Domain.Generic.Contracts.Requests.Update;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity;

public class UpdateIdentityCmd : UpdateIdentityRequest, IRequest<CmdResponseBO<UpdateIdentityCmd>>
{
    
}