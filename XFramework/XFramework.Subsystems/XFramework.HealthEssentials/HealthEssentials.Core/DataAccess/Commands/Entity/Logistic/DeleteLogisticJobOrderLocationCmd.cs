using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

public class DeleteLogisticJobOrderLocationCmd : DeleteLogisticJobOrderLocationRequest, IRequest<CmdResponse<DeleteLogisticJobOrderLocationCmd>>
{
    
}