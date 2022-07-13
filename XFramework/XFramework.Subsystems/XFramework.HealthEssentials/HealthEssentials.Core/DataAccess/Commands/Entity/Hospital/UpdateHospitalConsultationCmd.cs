using HealthEssentials.Domain.Generics.Contracts.Requests.Hospital.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

public class UpdateHospitalConsultationCmd : UpdateHospitalConsultationRequest, IRequest<CmdResponse<UpdateHospitalConsultationCmd>>
{
    
}