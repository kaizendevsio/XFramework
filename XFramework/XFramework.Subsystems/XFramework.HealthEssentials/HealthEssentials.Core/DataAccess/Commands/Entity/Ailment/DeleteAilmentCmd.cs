using HealthEssentials.Domain.Generics.Contracts.Requests.Ailment.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

public class DeleteAilmentCmd : DeleteAilmentRequest, IRequest<CmdResponse<DeleteAilmentCmd>>
{
    
}