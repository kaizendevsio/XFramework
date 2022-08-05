using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;

public class UpdateDoctorTagCmd : UpdateDoctorTagRequest, IRequest<CmdResponse<UpdateDoctorTagCmd>>
{
    
}