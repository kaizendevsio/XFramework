using HealthEssentials.Domain.Generics.Contracts.Requests.Unit.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Unit;

public class UpdateUnitEntityCmd : UpdateUnitEntityRequest, IRequest<CmdResponse<UpdateUnitEntityCmd>>
{
    
}