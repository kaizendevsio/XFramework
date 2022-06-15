using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class UpdateLogisticRiderHandler : CommandBaseHandler, IRequestHandler<UpdateLogisticRiderCmd, CmdResponse<UpdateLogisticRiderCmd>>
{
    public UpdateLogisticRiderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateLogisticRiderCmd>> Handle(UpdateLogisticRiderCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}