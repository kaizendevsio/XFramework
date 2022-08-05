using HealthEssentials.Domain.Generics.Contracts.Requests.Vendor.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Vendor;

public class CreateVendorCmd : CreateVendorRequest, IRequest<CmdResponse<CreateVendorCmd>>
{
    
}