using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class UpdateLogisticRiderHandleHandler : CommandBaseHandler, IRequestHandler<UpdateLogisticRiderHandleCmd, CmdResponse<UpdateLogisticRiderHandleCmd>>
{
    public UpdateLogisticRiderHandleHandler()
    {
        
    }
    public async Task<CmdResponse<UpdateLogisticRiderHandleCmd>> Handle(UpdateLogisticRiderHandleCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}