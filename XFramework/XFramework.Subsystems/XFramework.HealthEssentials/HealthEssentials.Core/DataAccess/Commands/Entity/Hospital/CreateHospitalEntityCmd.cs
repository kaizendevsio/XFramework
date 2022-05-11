using HealthEssentials.Domain.Generics.Contracts.Requests.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

public class CreateHospitalEntityCmd : CreateHospitalEntityRequest, IRequest<CmdResponse<CreateHospitalEntityRequest>>
{
    
}