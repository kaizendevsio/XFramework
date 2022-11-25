using HealthEssentials.Domain.Generics.Contracts.Requests.Medicine.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;

public class CreateMedicineCmd : CreateMedicineRequest, IRequest<CmdResponse<CreateMedicineCmd>>
{
    
}