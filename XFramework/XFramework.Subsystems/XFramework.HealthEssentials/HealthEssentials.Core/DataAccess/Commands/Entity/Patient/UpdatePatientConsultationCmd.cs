using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

public class UpdatePatientConsultationCmd : UpdatePatientConsultationRequest, IRequest<CmdResponse<UpdatePatientConsultationCmd>>
{
    
}