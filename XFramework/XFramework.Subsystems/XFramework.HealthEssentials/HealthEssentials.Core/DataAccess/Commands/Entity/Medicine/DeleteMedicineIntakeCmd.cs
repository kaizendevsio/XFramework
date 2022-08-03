using HealthEssentials.Domain.Generics.Contracts.Requests.Medicine.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;

public class DeleteMedicineIntakeCmd : DeleteMedicineIntakeRequest, IRequest<CmdResponse<DeleteMedicineIntakeCmd>>
{
    
}