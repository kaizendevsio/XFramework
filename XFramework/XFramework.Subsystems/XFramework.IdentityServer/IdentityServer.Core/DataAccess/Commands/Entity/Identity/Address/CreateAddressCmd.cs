using IdentityServer.Domain.Generic.Contracts.Requests.Create.Address;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity.Address;

public class CreateAddressCmd : CreateAddressRequest, IRequest<CmdResponse<CreateAddressCmd>>
{
    
}