using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;

public class CreateDoctorTagCmd : CreateDoctorTagRequest, IRequest<CmdResponse<CreateDoctorTagCmd>>
{
    
}