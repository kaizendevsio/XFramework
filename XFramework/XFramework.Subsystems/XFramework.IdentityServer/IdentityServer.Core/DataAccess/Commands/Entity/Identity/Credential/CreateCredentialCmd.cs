using IdentityServer.Domain.Generic.Contracts.Requests.Create;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity.Credential;

public class CreateCredentialCmd : CreateCredentialRequest, IRequest<CmdResponse<CreateCredentialCmd>>
{

}