using IdentityServer.Domain.Generic.Contracts.Requests.Create;
using IdentityServer.Domain.Generic.Enums;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity.Credential;

public class CreateCredentialCmd : CreateCredentialRequest, IRequest<CmdResponse<CreateCredentialCmd>>
{

}