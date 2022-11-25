using HealthEssentials.Domain.Generics.Contracts.Requests.Vendor.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Vendor;

public class UpdateVendorCmd : UpdateVendorRequest, IRequest<CmdResponse<UpdateVendorCmd>>
{
    
}