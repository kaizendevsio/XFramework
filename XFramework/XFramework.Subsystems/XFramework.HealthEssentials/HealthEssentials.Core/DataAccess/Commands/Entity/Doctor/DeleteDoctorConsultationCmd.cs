using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;

public class DeleteDoctorConsultationCmd : DeleteDoctorConsultationRequest, IRequest<CmdResponse<DeleteDoctorConsultationCmd>>
{
    
}