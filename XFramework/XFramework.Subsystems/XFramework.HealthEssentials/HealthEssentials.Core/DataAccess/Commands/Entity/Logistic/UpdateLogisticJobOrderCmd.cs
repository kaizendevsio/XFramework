using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

public class UpdateLogisticJobOrderCmd : UpdateLogisticJobOrderRequest, IRequest<CmdResponse<UpdateLogisticJobOrderCmd>>
{
    
}