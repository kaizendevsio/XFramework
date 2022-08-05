using HealthEssentials.Domain.Generics.Contracts.Requests.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

public class CreateHospitalEntityGroupCmd : CreateHospitalEntityGroupRequest, IRequest<CmdResponse<CreateHospitalEntityGroupCmd>>
{
    
}