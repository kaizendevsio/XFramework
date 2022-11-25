using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

public class UpdateLaboratoryTagCmd : UpdateLaboratoryTagRequest, IRequest<CmdResponse<UpdateLaboratoryTagCmd>>
{
    
}