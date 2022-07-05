using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;

public class UpdateDoctorCmd : UpdateDoctorRequest, IRequest<CmdResponse<UpdateDoctorCmd>>
{
    
}