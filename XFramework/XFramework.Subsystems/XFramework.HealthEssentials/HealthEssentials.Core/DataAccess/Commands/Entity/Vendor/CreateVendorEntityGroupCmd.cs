using HealthEssentials.Domain.Generics.Contracts.Requests.Vendor.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Vendor;

public class CreateVendorEntityGroupCmd : CreateVendorEntityGroupRequest, IRequest<CmdResponse<CreateVendorEntityGroupCmd>>
{
    
}