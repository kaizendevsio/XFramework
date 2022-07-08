using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

public class CreateLaboratoryLocationCmd : CreateLaboratoryLocationRequest, IRequest<CmdResponse<CreateLaboratoryLocationCmd>>
{
    
}