using HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.MetaData;

public class UpdateMetaDataEntityCmd : UpdateMetaDataEntityRequest, IRequest<CmdResponse<UpdateMetaDataEntityCmd>>
{
    
}