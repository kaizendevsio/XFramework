using IdentityServer.Domain.Generic.Contracts.Requests.Delete.Location;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity.Location;

public class DeleteLocationCmd : DeleteLocationRequest, IRequest<CmdResponse<DeleteLocationCmd>>
{
    
}