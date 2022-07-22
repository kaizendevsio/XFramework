using HealthEssentials.Domain.Generics.Contracts.Requests.Ailment.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

public class DeleteAilmentEntityCmd : DeleteAilmentEntityRequest, IRequest<CmdResponse<DeleteAilmentEntityCmd>>
{
    
}