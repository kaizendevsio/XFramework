using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;

public class DeleteDoctorCmd : DeleteDoctorRequest, IRequest<CmdResponse<DeleteDoctorCmd>>
{
    
}