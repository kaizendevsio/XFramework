using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

public class DeletePatientAilmentDetailCmd : DeletePatientAilmentDetailRequest, IRequest<CmdResponse<DeletePatientAilmentDetailCmd>>
{
    
}