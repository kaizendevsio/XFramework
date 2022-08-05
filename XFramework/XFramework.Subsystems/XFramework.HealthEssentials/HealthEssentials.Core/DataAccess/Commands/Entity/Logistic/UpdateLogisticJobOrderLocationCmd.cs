using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

public class UpdateLogisticJobOrderLocationCmd : UpdateLogisticJobOrderLocationRequest, IRequest<CmdResponse<UpdateLogisticJobOrderLocationCmd>>
{
    
}