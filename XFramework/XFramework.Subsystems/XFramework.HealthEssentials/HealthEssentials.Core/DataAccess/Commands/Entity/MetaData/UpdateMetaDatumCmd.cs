using HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.MetaData;

public class UpdateMetaDatumCmd : UpdateMetaDatumRequest, IRequest<CmdResponse<UpdateMetaDatumCmd>>
{
    
}