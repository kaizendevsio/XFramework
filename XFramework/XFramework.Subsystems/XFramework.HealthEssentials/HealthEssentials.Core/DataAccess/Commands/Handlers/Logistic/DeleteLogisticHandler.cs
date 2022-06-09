using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class DeleteLogisticHandler : CommandBaseHandler, IRequestHandler<DeleteLogisticCmd, CmdResponse<DeleteLogisticCmd>>
{
    public DeleteLogisticHandler()
    {
        
    }
    public async Task<CmdResponse<DeleteLogisticCmd>> Handle(DeleteLogisticCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}