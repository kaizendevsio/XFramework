using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

public class CreatePatientConsultationCmd : CreatePatientConsultationRequest, IRequest<CmdResponse<CreatePatientConsultationCmd>>
{
    
}