using HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.MetaData;

public class DeleteMetaDataEntityCmd : DeleteMetaDataEntityRequest, IRequest<CmdResponse<DeleteMetaDataEntityCmd>>
{
    
}