using HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Create;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.MetaData;

public class CreateMetaDataEntityCmd : CreateMetaDataEntityRequest, IRequest<CmdResponse<CreateMetaDataEntityCmd>>
{
    
}