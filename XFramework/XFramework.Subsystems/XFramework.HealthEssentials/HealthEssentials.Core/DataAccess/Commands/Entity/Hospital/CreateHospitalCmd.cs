using HealthEssentials.Domain.Generics.Contracts.Requests.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

public class CreateHospitalCmd : CreateHospitalRequest, IRequest<CmdResponse<CreateHospitalRequest>>
{
    
}