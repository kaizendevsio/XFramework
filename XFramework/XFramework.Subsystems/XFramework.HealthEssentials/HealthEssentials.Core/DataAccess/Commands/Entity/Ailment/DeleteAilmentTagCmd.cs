using HealthEssentials.Domain.Generics.Contracts.Requests.Ailment;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

public class DeleteAilmentTagCmd : DeleteAilmentTagRequest, IRequest<CmdResponse<DeleteAilmentTagCmd>>
{
    
}