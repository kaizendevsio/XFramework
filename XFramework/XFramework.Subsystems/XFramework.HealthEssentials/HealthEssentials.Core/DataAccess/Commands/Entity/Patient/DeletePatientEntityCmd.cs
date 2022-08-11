using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

public class DeletePatientEntityCmd : DeletePatientEntityRequest, IRequest<CmdResponse<DeletePatientEntityCmd>>
{
    
}