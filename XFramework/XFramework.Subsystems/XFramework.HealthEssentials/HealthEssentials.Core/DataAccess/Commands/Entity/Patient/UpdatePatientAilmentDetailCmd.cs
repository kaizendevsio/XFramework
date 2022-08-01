using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

public class UpdatePatientAilmentDetailCmd : UpdatePatientAilmentDetailRequest, IRequest<CmdResponse<UpdatePatientAilmentDetailCmd>>
{
    
}