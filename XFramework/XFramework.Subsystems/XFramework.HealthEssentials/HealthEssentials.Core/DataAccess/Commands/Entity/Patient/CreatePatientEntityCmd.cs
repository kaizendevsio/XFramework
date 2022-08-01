using HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

public class CreatePatientEntityCmd : CreatePatientEntityRequest, IRequest<CmdResponse<CreatePatientEntityCmd>>
{
    
}