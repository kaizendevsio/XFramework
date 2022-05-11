using HealthEssentials.Domain.Generics.Contracts.Requests.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

public class CreateHospitalEntityGroupCmd : CreateHospitalEntityGroupRequest, IRequest<CmdResponse<CreateHospitalEntityGroupRequest>>
{
    
}