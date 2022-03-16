using IdentityServer.Domain.Generic.Contracts.Requests.Update.Address;

namespace IdentityServer.Core.DataAccess.Commands.Entity.Identity.Address;

public class UpdateAddressCmd : UpdateAddressRequest, IRequest<CmdResponse<UpdateAddressCmd>>
{
    
}