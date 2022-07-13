using HealthEssentials.Domain.Generics.Contracts.Requests.Hospital;
using HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

public class CreateHospitalEntityCmd : CreateHospitalEntityRequest, IRequest<CmdResponse<CreateHospitalEntityCmd>>
{
    
}