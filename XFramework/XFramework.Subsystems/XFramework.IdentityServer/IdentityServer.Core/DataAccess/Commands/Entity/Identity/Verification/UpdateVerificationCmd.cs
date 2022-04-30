using IdentityServer.Domain.Generic.Contracts.Requests.Update;
using IdentityServer.Domain.Generic.Contracts.Requests.Update.Verification;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity.Verification;

public class UpdateVerificationCmd : UpdateVerificationRequest, IRequest<CmdResponse>
{
    
}