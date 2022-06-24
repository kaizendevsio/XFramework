using HealthEssentials.Domain.Generics.Contracts.Requests.Patient;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

public class CreatePatientCmd : CreatePatientRequest, IRequest<CmdResponse<CreatePatientCmd>>
{
    
}