using CreateLaboratoryServiceRequest = HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Create.CreateLaboratoryServiceRequest;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

public class CreateLaboratoryServiceCmd : CreateLaboratoryServiceRequest, IRequest<CmdResponse<CreateLaboratoryServiceCmd>>
{
    
}