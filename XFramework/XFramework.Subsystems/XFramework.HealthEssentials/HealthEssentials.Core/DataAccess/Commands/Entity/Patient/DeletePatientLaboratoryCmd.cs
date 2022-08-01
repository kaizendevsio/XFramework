using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

public class DeletePatientLaboratoryCmd : DeletePatientLaboratoryRequest, IRequest<CmdResponse<DeletePatientLaboratoryCmd>>
{
    
}