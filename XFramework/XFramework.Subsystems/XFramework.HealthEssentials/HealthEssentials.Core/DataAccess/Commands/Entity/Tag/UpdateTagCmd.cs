using HealthEssentials.Domain.Generics.Contracts.Requests.Tag.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Tag;

public class UpdateTagCmd : UpdateTagRequest, IRequest<CmdResponse<UpdateTagCmd>>
{
    
}