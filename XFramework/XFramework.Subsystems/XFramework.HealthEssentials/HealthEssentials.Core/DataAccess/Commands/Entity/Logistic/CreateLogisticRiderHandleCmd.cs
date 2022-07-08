using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Create;
using XFramework.Domain.Generic.Enums;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

public class CreateLogisticRiderHandleCmd : CreateLogisticRiderHandleRequest, IRequest<CmdResponse<CreateLogisticRiderHandleCmd>>
{
    
}