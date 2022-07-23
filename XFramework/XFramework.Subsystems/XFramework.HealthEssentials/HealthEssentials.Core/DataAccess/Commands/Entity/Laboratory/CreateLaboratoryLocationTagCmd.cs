using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

public class CreateLaboratoryLocationTagCmd : CreateLaboratoryLocationTagRequest, IRequest<CmdResponse<CreateLaboratoryLocationTagCmd>>
{
    
}