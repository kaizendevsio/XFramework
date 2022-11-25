using HealthEssentials.Core.DataAccess.Commands.Entity.Availability;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Availability;

public class CreateAvailabilityHandler : CommandBaseHandler, IRequestHandler<CreateAvailabilityCmd, CmdResponse<CreateAvailabilityCmd>>
{
    public CreateAvailabilityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateAvailabilityCmd>> Handle(CreateAvailabilityCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}