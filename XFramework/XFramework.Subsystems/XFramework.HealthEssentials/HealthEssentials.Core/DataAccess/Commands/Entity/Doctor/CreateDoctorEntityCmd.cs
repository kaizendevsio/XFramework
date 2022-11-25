using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;

public class CreateDoctorEntityCmd : CreateDoctorEntityRequest, IRequest<CmdResponse<CreateDoctorEntityCmd>>
{
    
}