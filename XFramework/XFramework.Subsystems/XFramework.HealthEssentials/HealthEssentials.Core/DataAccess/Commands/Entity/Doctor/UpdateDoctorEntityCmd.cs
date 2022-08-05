using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;

public class UpdateDoctorEntityCmd : UpdateDoctorEntityRequest, IRequest<CmdResponse<UpdateDoctorEntityCmd>>
{
    
}