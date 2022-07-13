using HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.MetaData;

public class DeleteMetaDataEntityGroupCmd : DeleteMetaDataEntityGroupRequest, IRequest<CmdResponse<DeleteMetaDataEntityGroupCmd>>
{
    
}