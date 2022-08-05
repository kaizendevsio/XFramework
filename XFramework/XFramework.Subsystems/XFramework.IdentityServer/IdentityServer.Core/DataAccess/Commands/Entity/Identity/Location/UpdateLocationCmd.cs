using IdentityServer.Domain.Generic.Contracts.Requests.Update.Location;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity.Location;

public class UpdateLocationCmd : UpdateLocationRequest, IRequest<CmdResponse<UpdateLocationCmd>>
{
    
}