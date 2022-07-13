using CreateLaboratoryServiceRequest = HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Create.CreateLaboratoryServiceRequest;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

public class CreateLaboratoryServiceCmd : Domain.Generics.Contracts.Requests.Laboratory.Create.CreateLaboratoryServiceRequest, IRequest<CmdResponse<CreateLaboratoryServiceCmd>>
{
    
}