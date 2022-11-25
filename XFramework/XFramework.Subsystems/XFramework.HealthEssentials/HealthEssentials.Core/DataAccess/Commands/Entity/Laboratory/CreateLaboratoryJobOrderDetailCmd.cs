using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

public class CreateLaboratoryJobOrderDetailCmd : CreateLaboratoryJobOrderDetailRequest, IRequest<CmdResponse<CreateLaboratoryJobOrderDetailCmd>>
{
    
}