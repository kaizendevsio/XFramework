using HealthEssentials.Domain.Generics.Contracts.Requests.Patient;
using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

public class CreatePatientCmd : CreatePatientRequest, IRequest<CmdResponse<CreatePatientCmd>>
{
    
}