using HealthEssentials.Domain.Generics.Contracts.Requests.Medicine.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;

public class UpdateMedicineTagCmd : UpdateMedicineTagRequest, IRequest<CmdResponse<UpdateMedicineTagCmd>>
{
    
}