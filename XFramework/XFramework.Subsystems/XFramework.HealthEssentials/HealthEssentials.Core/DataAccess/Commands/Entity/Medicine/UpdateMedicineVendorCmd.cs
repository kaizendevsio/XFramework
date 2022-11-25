using HealthEssentials.Domain.Generics.Contracts.Requests.Medicine.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;

public class UpdateMedicineVendorCmd : UpdateMedicineVendorRequest, IRequest<CmdResponse<UpdateMedicineVendorCmd>>
{
    
}