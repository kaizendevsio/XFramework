using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class DeleteLogisticRiderHandler : CommandBaseHandler, IRequestHandler<DeleteLogisticRiderCmd, CmdResponse<DeleteLogisticRiderCmd>>
{
    public DeleteLogisticRiderHandler()
    {
        
    }
    public async Task<CmdResponse<DeleteLogisticRiderCmd>> Handle(DeleteLogisticRiderCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}