using HealthEssentials.Domain.Generics.Contracts.Requests.Ailment.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

public class UpdateAilmentTagCmd : UpdateAilmentTagRequest, IRequest<CmdResponse<UpdateAilmentTagCmd>>
{
    
}