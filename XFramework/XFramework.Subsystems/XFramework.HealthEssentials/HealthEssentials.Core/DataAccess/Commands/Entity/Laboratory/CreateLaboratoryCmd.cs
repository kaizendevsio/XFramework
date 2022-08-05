using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

public class CreateLaboratoryCmd : CreateLaboratoryRequest, IRequest<CmdResponse<CreateLaboratoryCmd>>
{
}