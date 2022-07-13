using HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

public class UpdateHospitalEntityGroupCmd : UpdateHospitalEntityGroupRequest, IRequest<CmdResponse<UpdateHospitalEntityGroupCmd>>
{
    
}