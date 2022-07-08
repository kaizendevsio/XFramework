using HealthEssentials.Domain.Generics.Contracts.Requests.Availability;

namespace HealthEssentials.Core.DataAccess.Commands.Entity.Availability;

public class UpdateAvailabilityCmd : UpdateAvailabilityRequest, IRequest<CmdResponse<UpdateAvailabilityCmd>>
{
    
}