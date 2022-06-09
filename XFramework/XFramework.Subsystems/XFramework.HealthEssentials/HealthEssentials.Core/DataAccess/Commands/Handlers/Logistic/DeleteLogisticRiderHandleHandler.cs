using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class DeleteLogisticRiderHandleHandler : CommandBaseHandler, IRequestHandler<DeleteLogisticRiderHandleCmd, CmdResponse<DeleteLogisticRiderHandleCmd>>
{
    public DeleteLogisticRiderHandleHandler()
    {
        
    }
    public async Task<CmdResponse<DeleteLogisticRiderHandleCmd>> Handle(DeleteLogisticRiderHandleCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}