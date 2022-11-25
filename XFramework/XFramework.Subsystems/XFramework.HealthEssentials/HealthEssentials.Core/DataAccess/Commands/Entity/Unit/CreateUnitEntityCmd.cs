using HealthEssentials.Domain.Generics.Contracts.Requests.Unit.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Unit;

public class CreateUnitEntityCmd : CreateUnitEntityRequest, IRequest<CmdResponse<CreateUnitEntityCmd>>
{
    
}