using IdentityServer.Domain.Generic.Contracts.Requests.Create.Location;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity.Location;

public class CreateLocationCmd : CreateLocationRequest, IRequest<CmdResponse<CreateLocationCmd>>
{
    
}