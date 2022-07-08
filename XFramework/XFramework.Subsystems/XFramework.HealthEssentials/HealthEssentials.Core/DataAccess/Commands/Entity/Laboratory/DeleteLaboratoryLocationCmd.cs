using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

public class DeleteLaboratoryLocationCmd : DeleteLaboratoryLocationRequest, IRequest<CmdResponse<DeleteLaboratoryLocationCmd>>
{
    
}