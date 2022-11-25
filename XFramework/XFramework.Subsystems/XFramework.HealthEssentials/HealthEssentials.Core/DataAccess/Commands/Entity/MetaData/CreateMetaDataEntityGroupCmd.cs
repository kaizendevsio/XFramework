using HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.MetaData;

public class CreateMetaDataEntityGroupCmd : CreateMetaDataEntityGroupRequest, IRequest<CmdResponse<CreateMetaDataEntityGroupCmd>>
{
    
}