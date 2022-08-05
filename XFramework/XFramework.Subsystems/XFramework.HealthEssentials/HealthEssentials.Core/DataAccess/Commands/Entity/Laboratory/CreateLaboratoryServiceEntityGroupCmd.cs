using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

public class CreateLaboratoryServiceEntityGroupCmd : CreateLaboratoryServiceEntityGroupRequest, IRequest<CmdResponse<CreateLaboratoryServiceEntityGroupCmd>>
{
}