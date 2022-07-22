using HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;

public class UpdateDoctorConsultationJobOrderCmd : UpdateDoctorConsultationJobOrderRequest, IRequest<CmdResponse<UpdateDoctorConsultationJobOrderCmd>>
{
    
}