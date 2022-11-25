using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

public class UpdateLogisticEntityCmd : UpdateLogisticEntityRequest, IRequest<CmdResponse<UpdateLogisticEntityCmd>>
{
    
}