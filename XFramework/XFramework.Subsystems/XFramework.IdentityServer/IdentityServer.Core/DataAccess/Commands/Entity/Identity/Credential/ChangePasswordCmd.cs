using IdentityServer.Domain.Generic.Contracts.Requests.Update;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity.Credential;

public class ChangePasswordCmd : UpdatePasswordRequest, IRequest<CmdResponse<ChangePasswordCmd>>
{
    
}