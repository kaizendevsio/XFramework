using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

public class DeleteLaboratoryServiceCmd : DeleteLaboratoryServiceRequest, IRequest<CmdResponse<DeleteLaboratoryServiceCmd>>
{
    
}