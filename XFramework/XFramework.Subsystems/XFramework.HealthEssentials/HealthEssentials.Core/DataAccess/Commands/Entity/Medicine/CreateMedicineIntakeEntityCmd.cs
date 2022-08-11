using HealthEssentials.Domain.Generics.Contracts.Requests.Medicine.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;

public class CreateMedicineIntakeEntityCmd : CreateMedicineIntakeEntityRequest, IRequest<CmdResponse<CreateMedicineIntakeEntityCmd>>
{
    
}