using HealthEssentials.Core.DataAccess.Commands.Entity.Availability;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Availability;

public class UpdateAvailabilityHandler : CommandBaseHandler, IRequestHandler<UpdateAvailabilityCmd, CmdResponse<UpdateAvailabilityCmd>>
{
    public UpdateAvailabilityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateAvailabilityCmd>> Handle(UpdateAvailabilityCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}