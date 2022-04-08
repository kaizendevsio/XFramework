using IdentityServer.Domain.Generic.Contracts.Requests.Delete.Address;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity.Address;

public class DeleteAddressCmd : DeleteAddressRequest, IRequest<CmdResponse<DeleteAddressCmd>>
{
    
}