using HealthEssentials.Domain.Generics.Contracts.Requests.Vendor.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Vendor;

public class DeleteVendorEntityGroupCmd : DeleteVendorEntityGroupRequest, IRequest<CmdResponse<DeleteVendorEntityGroupCmd>>
{
    
}