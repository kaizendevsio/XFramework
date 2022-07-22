using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Delete;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

public class DeleteLogisticJobOrderDetailCmd : DeleteLogisticJobOrderDetailRequest, IRequest<CmdResponse<DeleteLogisticJobOrderDetailCmd>>
{
    
}