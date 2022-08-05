using HealthEssentials.Domain.Generics.Contracts.Requests.Patient;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

public class DeletePatientCmd : DeletePatientRequest, IRequest<CmdResponse<DeletePatientCmd>>
{
    
}