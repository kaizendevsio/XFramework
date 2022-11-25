using HealthEssentials.Domain.Generics.Contracts.Requests.Unit.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Unit;

public class DeleteUnitEntityGroupCmd : DeleteUnitEntityGroupRequest, IRequest<CmdResponse<DeleteUnitEntityGroupCmd>>
{
    
}