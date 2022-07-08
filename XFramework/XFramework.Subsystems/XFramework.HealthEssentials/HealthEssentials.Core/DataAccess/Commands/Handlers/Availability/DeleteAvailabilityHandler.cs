using HealthEssentials.Core.DataAccess.Commands.Entity.Availability;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Availability;

public class DeleteAvailabilityHandler : CommandBaseHandler, IRequestHandler<DeleteAvailabilityCmd, CmdResponse<DeleteAvailabilityCmd>>
{
    public DeleteAvailabilityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteAvailabilityCmd>> Handle(DeleteAvailabilityCmd request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}