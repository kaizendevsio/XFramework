using HealthEssentials.Domain.Generics.Contracts.Requests.Tag.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Tag;

public class CreateTagCmd : CreateTagRequest, IRequest<CmdResponse<CreateTagCmd>>
{
    
}