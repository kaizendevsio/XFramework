using IdentityServer.Domain.Generic.Contracts.Requests.Create;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity;

public class CreateIdentityCmd : CreateIdentityRequest, IRequest<CmdResponse<CreateIdentityCmd>>
{
    
}