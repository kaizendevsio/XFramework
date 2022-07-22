using HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.MetaData;

public class DeleteMetaDatumCmd : DeleteMetaDatumRequest, IRequest<CmdResponse<DeleteMetaDatumCmd>>
{
    
}