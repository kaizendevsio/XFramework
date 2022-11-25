using HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

public class UpdateHospitalServiceCmd : UpdateHospitalServiceRequest, IRequest<CmdResponse<UpdateHospitalServiceCmd>>
{
    
}