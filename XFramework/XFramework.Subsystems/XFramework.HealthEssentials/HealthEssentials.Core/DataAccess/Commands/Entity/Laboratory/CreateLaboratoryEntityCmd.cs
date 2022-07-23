using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

public class CreateLaboratoryEntityCmd : CreateLaboratoryEntityRequest, IRequest<CmdResponse<CreateLaboratoryEntityCmd>>
{
    
}