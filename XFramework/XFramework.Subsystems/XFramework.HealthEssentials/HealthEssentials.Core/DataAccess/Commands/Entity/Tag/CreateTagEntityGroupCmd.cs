using HealthEssentials.Domain.Generics.Contracts.Requests.Tag.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Tag;

public class CreateTagEntityGroupCmd : CreateTagEntityGroupRequest, IRequest<CmdResponse<CreateTagEntityGroupCmd>>
{
    
}