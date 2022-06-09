using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class UpdateLogisticHandler : CommandBaseHandler, IRequestHandler<UpdateLogisticCmd, CmdResponse<UpdateLogisticCmd>>
{
    public UpdateLogisticHandler()
    {
        
    }
    public async Task<CmdResponse<UpdateLogisticCmd>> Handle(UpdateLogisticCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}