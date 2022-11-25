using HealthEssentials.Domain.Generics.Contracts.Requests.Medicine.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;

public class DeleteMedicineEntityGroupCmd : DeleteMedicineEntityGroupRequest, IRequest<CmdResponse<DeleteMedicineEntityGroupCmd>>
{
    
}