using IdentityServer.Domain.Generic.Contracts.Requests.Update;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity.Credential;

public class UpdateCredentialCmd : UpdateCredentialRequest, IRequest<CmdResponse<UpdateCredentialCmd>>
{

}