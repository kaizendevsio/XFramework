using HealthEssentials.Domain.Generics.Contracts.Requests.Ailment.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

public class UpdateAilmentEntityGroupCmd : UpdateAilmentEntityGroupRequest, IRequest<CmdResponse<UpdateAilmentEntityGroupCmd>>, IRequest<CmdResponse<DeleteAilmentEntityCmd>>
{
    
}