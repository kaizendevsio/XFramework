using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;

public class DeleteDoctorEntityCmd : DeleteDoctorEntityRequest, IRequest<CmdResponse<DeleteDoctorEntityCmd>>
{
    
}