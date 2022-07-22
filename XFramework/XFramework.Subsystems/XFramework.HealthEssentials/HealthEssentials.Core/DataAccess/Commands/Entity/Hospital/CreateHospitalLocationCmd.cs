using HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

public class CreateHospitalLocationCmd : CreateHospitalLocationRequest, IRequest<CmdResponse<CreateHospitalLocationCmd>>
{
    
}