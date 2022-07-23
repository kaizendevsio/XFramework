using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

public class CreateLaboratoryJobOrderResultFileCmd : CreateLaboratoryJobOrderResultFileRequest, IRequest<CmdResponse<CreateLaboratoryJobOrderResultFileCmd>>
{
    
}