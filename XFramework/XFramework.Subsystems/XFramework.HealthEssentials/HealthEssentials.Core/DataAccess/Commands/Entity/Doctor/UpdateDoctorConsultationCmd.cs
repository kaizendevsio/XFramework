using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;

public class UpdateDoctorConsultationCmd : UpdateDoctorConsultationRequest, IRequest<CmdResponse<UpdateDoctorConsultationCmd>>
{
    
}