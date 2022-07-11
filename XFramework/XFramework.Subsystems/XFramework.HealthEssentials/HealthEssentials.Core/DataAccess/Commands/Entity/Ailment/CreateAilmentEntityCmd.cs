using HealthEssentials.Domain.Generics.Contracts.Requests.Ailment.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

public class CreateAilmentEntityCmd : CreateAilmentEntityRequest, IRequest<CmdResponse<CreateAilmentEntityCmd>>
{
    
}