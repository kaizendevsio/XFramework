using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Update;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

public class UpdateLogisticCmd : UpdateLogisticRequest, IRequest<CmdResponse<UpdateLogisticCmd>>
{
    
}