using HealthEssentials.Domain.Generics.Contracts.Requests.Tag.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Tag;

public class DeleteTagCmd : DeleteTagRequest, IRequest<CmdResponse<DeleteTagCmd>>
{
    
}