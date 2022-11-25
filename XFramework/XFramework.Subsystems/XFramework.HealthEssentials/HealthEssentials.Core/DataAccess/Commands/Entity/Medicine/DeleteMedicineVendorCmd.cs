using HealthEssentials.Domain.Generics.Contracts.Requests.Medicine.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;

public class DeleteMedicineVendorCmd : DeleteMedicineVendorRequest, IRequest<CmdResponse<DeleteMedicineVendorCmd>>
{
    
}