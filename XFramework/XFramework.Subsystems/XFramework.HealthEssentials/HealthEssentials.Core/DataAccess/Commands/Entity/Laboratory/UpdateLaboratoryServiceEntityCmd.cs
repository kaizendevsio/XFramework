using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

public class UpdateLaboratoryServiceEntityCmd : UpdateLaboratoryServiceEntityRequest, IRequest<CmdResponse<UpdateLaboratoryServiceEntityCmd>>
{
    
}