using HealthEssentials.Domain.Generics.Contracts.Requests.Unit.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Unit;

public class DeleteUnitCmd : DeleteUnitRequest, IRequest<CmdResponse<DeleteUnitCmd>>
{
    
}