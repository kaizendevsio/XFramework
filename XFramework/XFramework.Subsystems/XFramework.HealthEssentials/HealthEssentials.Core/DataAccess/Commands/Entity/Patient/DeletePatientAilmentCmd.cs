using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

public class DeletePatientAilmentCmd : DeletePatientAilmentRequest, IRequest<CmdResponse<DeletePatientAilmentCmd>>
{
    
}