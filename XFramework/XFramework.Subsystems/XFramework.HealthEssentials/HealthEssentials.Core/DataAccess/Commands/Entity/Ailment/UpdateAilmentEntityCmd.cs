using HealthEssentials.Domain.Generics.Contracts.Requests.Ailment.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

public class UpdateAilmentEntityCmd : UpdateAilmentEntityRequest, IRequest<CmdResponse<UpdateAilmentEntityCmd>>
{
    
}