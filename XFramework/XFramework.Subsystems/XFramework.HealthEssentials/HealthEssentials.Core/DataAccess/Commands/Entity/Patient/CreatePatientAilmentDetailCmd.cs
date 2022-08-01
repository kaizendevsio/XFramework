using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

public class CreatePatientAilmentDetailCmd : CreatePatientAilmentDetailRequest, IRequest<CmdResponse<CreatePatientAilmentDetailCmd>>
{
    
}