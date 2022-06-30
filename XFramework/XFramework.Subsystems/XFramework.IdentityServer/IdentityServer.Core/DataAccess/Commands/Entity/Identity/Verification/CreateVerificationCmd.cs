using IdentityServer.Domain.Generic.Contracts.Requests.Create.Verification;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity.Verification;

public class CreateVerificationCmd : CreateVerificationRequest, IRequest<CmdResponse>
{
    
}