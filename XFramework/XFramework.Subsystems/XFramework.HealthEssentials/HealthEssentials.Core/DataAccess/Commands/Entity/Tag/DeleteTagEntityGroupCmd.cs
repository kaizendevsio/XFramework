using HealthEssentials.Domain.Generics.Contracts.Requests.Tag.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Tag;

public class DeleteTagEntityGroupCmd : DeleteTagEntityGroupRequest, IRequest<CmdResponse<DeleteTagEntityGroupCmd>>
{
    
}