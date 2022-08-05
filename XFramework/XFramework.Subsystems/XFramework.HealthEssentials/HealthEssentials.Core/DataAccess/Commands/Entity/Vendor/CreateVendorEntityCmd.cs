using HealthEssentials.Domain.Generics.Contracts.Requests.Vendor.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Vendor;

public class CreateVendorEntityCmd : CreateVendorEntityRequest, IRequest<CmdResponse<CreateVendorEntityCmd>>
{
    
}